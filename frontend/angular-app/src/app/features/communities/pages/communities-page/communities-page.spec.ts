import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { CommunityDto } from '../../../../core/models/api.model';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunitiesPage } from './communities-page';

const temporaryCommunity: CommunityDto = {
  id: 'temporary-7',
  municipality: 'Municipio', department: 'Departamento', country: 'Guatemala', latitude: 15, longitude: -90, sensorCount: 2,
  name: 'Comunidad temporal',
  location: 'Guatemala',
  description: 'Disponible para pruebas',
  isActive: true,
  createdAt: '2026-08-24T12:00:00Z',
};

describe('CommunitiesPage', () => {
  let fixture: ComponentFixture<CommunitiesPage>;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommunitiesPage],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { session: () => ({ role: 'Administrator' }), canOperate: () => true } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(CommunitiesPage);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(r => r.url === '/api/communities').flush({ data: [temporaryCommunity], totalCount: 1, totalPages: 1 });
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('renders readable middle-dot separators between administrative fields', () => {
    fixture.componentInstance.communities = [{ ...temporaryCommunity, municipality: 'Guatemala', department: 'Guatemala', country: 'Guatemala' }];
    fixture.detectChanges();
    const card = fixture.nativeElement.querySelector('.community-card');
    expect(card.textContent).toContain('Guatemala · Guatemala · Guatemala');
    expect(card.textContent).not.toContain('Guatemala ? Guatemala');
  });

  it('shows administrative fields, state and sensor count', () => {
    expect(fixture.nativeElement.textContent).toContain('Municipio');
    expect(fixture.nativeElement.textContent).toContain('Departamento');
    expect(fixture.nativeElement.textContent).toContain('Guatemala');
    expect(fixture.nativeElement.textContent).toContain('2 sensores asociados');
    expect(fixture.nativeElement.textContent).toContain('Activa');
  });

  it('rejects invalid coordinates before sending a write', () => {
    const c = fixture.componentInstance;
    c.name = 'Comunidad'; c.location = 'Referencia'; c.municipality = 'M'; c.department = 'D'; c.country = 'P'; c.latitude = 91; c.longitude = -90;
    c.saveCommunity(); expect(c.error).toBeTruthy(); http.expectNone(r => r.method === 'POST');
  });

  it('applies server filters and preserves them on the next page', () => {
    const c = fixture.componentInstance; c.search = 'Rural'; c.statusFilter = 'false'; c.municipalityFilter = 'M'; c.departmentFilter = 'D'; c.applyFilters();
    const first = http.expectOne(r => r.url === '/api/communities');
    expect(first.request.params.get('search')).toBe('Rural'); expect(first.request.params.get('isActive')).toBe('false');
    expect(first.request.params.get('municipality')).toBe('M'); expect(first.request.params.get('department')).toBe('D');
    first.flush({ data: [temporaryCommunity], totalCount: 30, totalPages: 2 });
    c.search = 'Draft'; c.goToPage(2);
    const next = http.expectOne(r => r.url === '/api/communities');
    expect(next.request.params.get('page')).toBe('2'); expect(next.request.params.get('search')).toBe('Rural');
    next.flush({ data: [], totalCount: 30, totalPages: 2 });
  });

  it('deactivates after confirmation and refreshes the listing', () => {
    spyOn(window, 'confirm').and.returnValue(true); fixture.componentInstance.changeStatus(temporaryCommunity);
    const request = http.expectOne('/api/communities/temporary-7/status'); expect(request.request.method).toBe('PATCH'); expect(request.request.body).toEqual({ isActive: false });
    request.flush({ ...temporaryCommunity, isActive: false });
    http.expectOne(r => r.url === '/api/communities').flush({ data: [{ ...temporaryCommunity, isActive: false }], totalCount: 1, totalPages: 1 });
    fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Inactiva');
  });

  it('does not change status when confirmation is cancelled', () => {
    spyOn(window, 'confirm').and.returnValue(false); fixture.componentInstance.changeStatus(temporaryCommunity);
    http.expectNone('/api/communities/temporary-7/status');
  });

  it('hides forms and write actions for ConsultationUser', () => {
    TestBed.inject(AuthService).canOperate = () => false; fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input[name="name"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('.community-card__actions')).toBeNull();
    fixture.componentInstance.saveCommunity(); fixture.componentInstance.changeStatus(temporaryCommunity);
    http.expectNone(r => r.method !== 'GET');
  });

  it('opens the visible edit state with the current community values', fakeAsync(() => {
    (fixture.nativeElement.querySelector('.community-card__actions button') as HTMLButtonElement).click();
    fixture.detectChanges();
    tick();

    expect(fixture.nativeElement.querySelector('#community-form-title').textContent).toContain('Editar comunidad');
    expect((fixture.nativeElement.querySelector('input[name="name"]') as HTMLInputElement).value).toBe(temporaryCommunity.name);
    expect((fixture.nativeElement.querySelector('input[name="location"]') as HTMLInputElement).value).toBe(temporaryCommunity.location);
    expect((fixture.nativeElement.querySelector('textarea[name="description"]') as HTMLTextAreaElement).value).toBe(temporaryCommunity.description ?? '');
    expect(fixture.nativeElement.querySelector('.community-card--editing')).not.toBeNull();
  }));

  it('sends PUT and updates the card without reloading', fakeAsync(() => {
    (fixture.nativeElement.querySelector('.community-card__actions button') as HTMLButtonElement).click();
    fixture.detectChanges();
    tick();
    const name = fixture.nativeElement.querySelector('input[name="name"]') as HTMLInputElement;
    name.value = 'Comunidad temporal editada';
    name.dispatchEvent(new Event('input'));
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));

    const request = http.expectOne('/api/communities/temporary-7');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.name).toBe('Comunidad temporal editada');
    request.flush({ ...temporaryCommunity, name: 'Comunidad temporal editada' });
    http.expectOne(r => r.url === '/api/communities').flush({ data: [{ ...temporaryCommunity, name: 'Comunidad temporal editada' }], totalCount: 1, totalPages: 1 });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.community-card h3').textContent).toContain('Comunidad temporal editada');
    expect(fixture.nativeElement.textContent).toContain('Comunidad actualizada correctamente.');
  }));

  it('cancels editing without sending a request or changing the card', fakeAsync(() => {
    (fixture.nativeElement.querySelector('.community-card__actions button') as HTMLButtonElement).click();
    fixture.detectChanges();
    tick();
    (fixture.nativeElement.querySelector('.secondary-button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#community-form-title').textContent).toContain('Registrar comunidad');
    expect(fixture.nativeElement.querySelector('.community-card h3').textContent).toContain(temporaryCommunity.name);
  }));
});
