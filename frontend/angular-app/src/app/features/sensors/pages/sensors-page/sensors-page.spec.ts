import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';
import { CommunityDto, SensorDto, SensorReadingDto, SensorType } from '../../../../core/models/api.model';
import { SensorsPage } from './sensors-page';
import { provideRouter } from '@angular/router';

describe('SensorsPage administration', () => {
  const community: CommunityDto = { id: 'community-1', name: 'Lanquín', location: 'Alta Verapaz', description: null, isActive: true, createdAt: '2026-08-01' };
  const sensor: SensorDto = { type: 'Temperature', unit: '\u00b0C', installationDate: '2026-01-01', description: 'Equipo', id: 'sensor-1', communityId: community.id, code: 'SEN-LAN-TEMP-01', name: 'Sensor de temperatura - Entrada principal', measurementType: 'Temperature', origin: 'Simulated', status: 'Active', location: 'Entrada principal', deviceCode: null, lastCommunicationAt: null, createdAt: '2026-08-01' };
  const reading: SensorReadingDto = { id: 'reading-1', sensorId: sensor.id, variable: 'Temperature', value: 25, unit: '°C', measuredAt: '2026-08-25', receivedAt: '2026-08-25', origin: 'Simulated' };
  let fixture: ComponentFixture<SensorsPage>;
  let component: SensorsPage;
  let role: ReturnType<typeof signal<{ role: string } | null>>;
  let sensorApi: jasmine.SpyObj<SensorApiService>;
  let readingApi: jasmine.SpyObj<SensorReadingApiService>;

  beforeEach(() => {
    role = signal<{ role: string } | null>({ role: 'Administrator' });
    sensorApi = jasmine.createSpyObj('SensorApiService', ['getPage', 'getByCommunity', 'create', 'update', 'changeStatus']);
    readingApi = jasmine.createSpyObj('SensorReadingApiService', ['getLatest', 'createManual']);
    sensorApi.getByCommunity.and.returnValue(of([sensor]));
    sensorApi.getPage.and.returnValue(of({ data: [sensor], totalCount: 1, totalPages: 1, pageIndex: 1, pageSize: 20, hasPrevious: false, hasNext: false }));
    readingApi.getLatest.and.returnValue(of(reading));
    TestBed.configureTestingModule({
      imports: [SensorsPage],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { session: role, canOperate: () => ['Administrator', 'Operator'].includes(role()?.role ?? '') } },
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

  const abbreviations: Record<SensorType, string> = { Temperature: 'TEMP', Humidity: 'HUM', WindSpeed: 'WIND', Rainfall: 'RAIN', RiverLevel: 'RIVER', ReservoirLevel: 'RIVER', SmokeFire: 'SMOKE', OtherEnvironmental: 'ENV' };
  for (const type of Object.keys(abbreviations) as SensorType[]) {
    it(`previews ${type} with the abbreviation used by the persisted code`, () => {
      component.sensorType = type; component.selectType(); fixture.detectChanges();
      expect(component.estimatedCode()).toBe(`SEN-LAN-${abbreviations[type]}-XX`);
      expect(fixture.nativeElement.querySelector('.generated-preview').textContent).toContain(`SEN-LAN-${abbreviations[type]}-XX`);
      expect(component.estimatedCode()).not.toContain('undefined');
    });
  }

  it('uses a safe preview fallback for an unknown type and community', () => {
    component.communities = [{ ...community, name: 'La Isla' }];
    component.sensorType = 'Unknown' as SensorType;
    expect(component.estimatedCode()).toBe('SEN-COM-VAR-XX');
    component.selectedCommunityId = '';
    expect(component.estimatedCode()).toBe('Selecciona una comunidad');
  });

  it('shows eight official types with the correct variable and unit', () => {
    expect(fixture.nativeElement.querySelectorAll('select[name="measurementType"] option').length).toBe(8);
    component.sensorType = 'ReservoirLevel'; component.selectType(); expect(component.measurementType).toBe('RiverOrReservoirLevel'); expect(component.unit).toBe('m');
    component.sensorType = 'SmokeFire'; component.selectType(); expect(component.measurementType).toBe('SmokeConcentration'); expect(component.unit).toBe('ppm');
  });

  it('sends complete fields and rejects a missing installation date', () => {
    component.location = 'Entrada'; component.installationDate = ''; component.createSensor(); expect(sensorApi.create).not.toHaveBeenCalled();
    component.installationDate = '2026-01-01'; component.name = 'Nombre'; component.code = 'CUSTOM'; component.description = 'Equipo';
    sensorApi.create.and.returnValue(of(sensor)); component.createSensor();
    expect(sensorApi.create).toHaveBeenCalledWith(jasmine.objectContaining({ name: 'Nombre', code: 'CUSTOM', description: 'Equipo', installationDate: '2026-01-01' }));
  });

  it('preserves applied filters when paging', () => {
    sensorApi.getPage.and.returnValue(of({ data: [sensor], totalCount: 30, totalPages: 2, pageIndex: 1, pageSize: 20, hasPrevious: false, hasNext: true }));
    component.typeFilter = 'SmokeFire'; component.statusFilter = 'false'; component.codeFilter = 'S-'; component.search = 'Rural'; component.applyFilters();
    expect(sensorApi.getPage).toHaveBeenCalledWith(jasmine.objectContaining({ communityId: community.id, type: 'SmokeFire', isActive: 'false', code: 'S-', search: 'Rural' }));
    component.search = 'Draft'; component.goToPage(2);
    expect(sensorApi.getPage).toHaveBeenCalledWith(jasmine.objectContaining({ page: 2, search: 'Rural' }));
  });

  it('shows installation date, description and community', () => {
    expect(fixture.nativeElement.textContent).toContain('2026-01-01'); expect(fixture.nativeElement.textContent).toContain('Equipo'); expect(fixture.nativeElement.textContent).toContain(community.name);
  });

  it('confirms deactivation and blocks manual readings while inactive', () => {
    spyOn(window, 'confirm').and.returnValue(true); sensorApi.changeStatus.and.returnValue(of({ ...sensor, status: 'Inactive' })); component.changeStatus(sensor);
    expect(sensorApi.changeStatus).toHaveBeenCalledWith(sensor.id, false);
    component.startManualReading({ ...sensor, status: 'Inactive' }); expect(component.readingSensor).toBeNull(); expect(component.error).toContain('inactivo');
  });

  it('cancels status changes without requests', () => {
    spyOn(window, 'confirm').and.returnValue(false); component.changeStatus(sensor); expect(sensorApi.changeStatus).not.toHaveBeenCalled();
  });

  it('uses dropdowns and only asks for understandable creation fields', () => {
    const form = fixture.nativeElement.querySelector('form.sensor-form') as HTMLFormElement;
    expect(form.querySelector('select[name="community"]')).toBeTruthy();
    expect(form.querySelector('select[name="measurementType"]')).toBeTruthy();
    expect(form.querySelector('input[name="location"]')).toBeTruthy();
    expect(form.querySelector('select[name="initialActive"]')).toBeTruthy();
    expect(form.querySelector('input[name="code"]')).toBeTruthy();
    expect(form.querySelector('input[name="name"]')).toBeTruthy();
    expect(form.querySelector('input[name="deviceCode"]')).toBeNull();
  });

  it('shows generated name and conceptual code preview', () => {
    component.location = 'Entrada principal'; fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sensor de temperatura - Entrada principal');
    expect(fixture.nativeElement.textContent).toContain('SEN-LAN-TEMP-XX');
  });

  it('creates with the minimal DTO and shows the actual returned code', () => {
    sensorApi.create.and.returnValue(of(sensor)); component.location = 'Entrada principal'; component.createSensor();
    expect(sensorApi.create).toHaveBeenCalledWith(jasmine.objectContaining({ communityId: community.id, measurementType: 'Temperature', location: 'Entrada principal', isActive: true, type: 'Temperature', installationDate: component.installationDate }));
    expect(component.success).toContain('SEN-LAN-TEMP-01');
  });

  it('edits only location and preserves the immutable code', () => {
    sensorApi.update.and.returnValue(of({ ...sensor, location: 'Sector norte', name: 'Sensor de temperatura - Sector norte' }));
    component.startEdit(sensor); component.editLocation = 'Sector norte'; component.saveEdit();
    expect(sensorApi.update).toHaveBeenCalledWith(sensor.id, jasmine.objectContaining({ location: 'Sector norte', name: sensor.name, code: sensor.code, type: 'Temperature', installationDate: '2026-01-01' }));
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
  for (const name of ['Operator', 'Query', 'User']) {
    it(`shows write controls only for operational role ${name}`, () => {
      role.set({ role: name }); fixture.detectChanges();
      expect(!!fixture.nativeElement.querySelector('[aria-labelledby="sensor-form-title"] form')).toBe(name === 'Operator');
      expect(component.canAdminister()).toBe(name === 'Operator');
    });
  }

});
