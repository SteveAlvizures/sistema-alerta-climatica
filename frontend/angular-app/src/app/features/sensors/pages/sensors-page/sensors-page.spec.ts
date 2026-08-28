import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';
import { CommunityDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { SensorsPage } from './sensors-page';
import { provideRouter } from '@angular/router';

describe('SensorsPage administration', () => {
  const community: CommunityDto = { id: 'community-1', name: 'Lanquín', location: 'Alta Verapaz', description: null, isActive: true, createdAt: '2026-08-01' };
  const sensor: SensorDto = { id: 'sensor-1', communityId: community.id, code: 'SEN-LAN-TEMP-01', name: 'Sensor de temperatura - Entrada principal', measurementType: 'Temperature', origin: 'Simulated', status: 'Active', location: 'Entrada principal', deviceCode: null, lastCommunicationAt: null, createdAt: '2026-08-01' };
  const reading: SensorReadingDto = { id: 'reading-1', sensorId: sensor.id, variable: 'Temperature', value: 25, unit: '°C', measuredAt: '2026-08-25', receivedAt: '2026-08-25', origin: 'Simulated' };
  let fixture: ComponentFixture<SensorsPage>;
  let component: SensorsPage;
  let role: ReturnType<typeof signal<{ role: string } | null>>;
  let sensorApi: jasmine.SpyObj<SensorApiService>;
  let readingApi: jasmine.SpyObj<SensorReadingApiService>;

  beforeEach(() => {
    role = signal<{ role: string } | null>({ role: 'Administrator' });
    sensorApi = jasmine.createSpyObj('SensorApiService', ['getByCommunity', 'create', 'update', 'changeStatus']);
    readingApi = jasmine.createSpyObj('SensorReadingApiService', ['getLatest', 'createManual']);
    sensorApi.getByCommunity.and.returnValue(of([sensor]));
    readingApi.getLatest.and.returnValue(of(reading));
    TestBed.configureTestingModule({
      imports: [SensorsPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { session: role } },
        { provide: CommunityApiService, useValue: { getAll: () => of([community]) } },
        { provide: SensorApiService, useValue: sensorApi },
        { provide: SensorReadingApiService, useValue: readingApi },
      ],
    });
    fixture = TestBed.createComponent(SensorsPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('uses dropdowns and only asks for understandable creation fields', () => {
    const form = fixture.nativeElement.querySelector('form.sensor-form') as HTMLFormElement;
    expect(form.querySelector('select[name="community"]')).toBeTruthy();
    expect(form.querySelector('select[name="measurementType"]')).toBeTruthy();
    expect(form.querySelector('input[name="location"]')).toBeTruthy();
    expect(form.querySelector('select[name="initialActive"]')).toBeTruthy();
    expect(form.querySelector('input[name="code"]')).toBeNull();
    expect(form.querySelector('input[name="name"]')).toBeNull();
    expect(form.querySelector('input[name="deviceCode"]')).toBeNull();
  });

  it('shows generated name and conceptual code preview', () => {
    component.location = 'Entrada principal'; fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sensor de temperatura - Entrada principal');
    expect(fixture.nativeElement.textContent).toContain('SEN-LAN-TEMP-XX');
  });

  it('creates with the minimal DTO and shows the actual returned code', () => {
    sensorApi.create.and.returnValue(of(sensor)); component.location = 'Entrada principal'; component.createSensor();
    expect(sensorApi.create).toHaveBeenCalledWith({ communityId: community.id, measurementType: 'Temperature', location: 'Entrada principal', isActive: true });
    expect(component.success).toContain('SEN-LAN-TEMP-01');
  });

  it('edits only location and preserves the immutable code', () => {
    sensorApi.update.and.returnValue(of({ ...sensor, location: 'Sector norte', name: 'Sensor de temperatura - Sector norte' }));
    component.startEdit(sensor); component.editLocation = 'Sector norte'; component.saveEdit();
    expect(sensorApi.update).toHaveBeenCalledWith(sensor.id, { location: 'Sector norte' });
    expect(component.sensors[0].code).toBe(sensor.code);
  });

  it('cancels edit and manual reading without requests', () => {
    component.startEdit(sensor); component.cancelEdit(); component.startManualReading(sensor); component.cancelManualReading();
    expect(sensorApi.update).not.toHaveBeenCalled(); expect(readingApi.createManual).not.toHaveBeenCalled();
  });

  it('hides manual reading and administration from Guest and User', () => {
    role.set(null); fixture.detectChanges(); expect(fixture.nativeElement.textContent).not.toContain('Registrar lectura');
    role.set({ role: 'User' }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Registrar lectura');
    expect(fixture.nativeElement.textContent).not.toContain('Crear sensor');
  });

  it('shows metadata and only requests value for a manual reading', () => {
    component.startManualReading(sensor); fixture.detectChanges();
    const form = fixture.nativeElement.querySelector('[aria-labelledby="manual-reading-title"] form') as HTMLFormElement;
    expect(form.textContent).toContain(sensor.name); expect(form.textContent).toContain(community.name);
    expect(form.textContent).toContain('Temperatura'); expect(form.textContent).toContain('°C');
    expect(form.querySelectorAll('input').length).toBe(1);
    expect(form.querySelector('input[name="manualValue"]')?.hasAttribute('required')).toBeTrue();
  });

  it('posts a manual reading and displays success', () => {
    readingApi.createManual.and.returnValue(of({ ...reading, value: 31.5 }));
    component.startManualReading(sensor); component.manualValue = 31.5; component.registerManualReading();
    expect(readingApi.createManual).toHaveBeenCalledWith(sensor.id, 31.5);
    expect(component.success).toBe('Lectura registrada correctamente.');
    expect(component.latestReadings[sensor.id]?.value).toBe(31.5);
  });
});
