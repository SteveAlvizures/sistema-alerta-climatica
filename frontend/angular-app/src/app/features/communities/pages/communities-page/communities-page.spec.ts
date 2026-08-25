import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { CommunityDto } from '../../../../core/models/api.model';
import { AuthService } from '../../../../core/services/auth.service';
import { CommunitiesPage } from './communities-page';

const temporaryCommunity: CommunityDto = {
  id: 'temporary-7',
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
        { provide: AuthService, useValue: { session: () => ({ role: 'Administrator' }) } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(CommunitiesPage);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/communities').flush([temporaryCommunity]);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

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
