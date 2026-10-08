import { provideHttpClient, withXhr } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../../../core/config/api.config';
import { PagedResponse } from '../../../../core/models/api.model';
import { AuthService } from '../../../../core/services/auth.service';
import { UserApiService, UserDto } from '../../../../core/services/user-api.service';
import { UsersPage } from './users-page';

describe('UsersPage administration', () => {
  const user: UserDto = { id: 'account-1', name: 'Academic Account', username: 'account', role: 'Operator',
    isActive: true, createdAt: '2026-10-06T12:00:00Z', lastAccessAt: null };
  let fixture: ComponentFixture<UsersPage>;
  let component: UsersPage;
  let http: HttpTestingController;
  let allowed: boolean;

  function result(data: UserDto[] = [user], extra: Partial<PagedResponse<UserDto>> = {}): PagedResponse<UserDto> {
    return { data, pageIndex: 1, pageSize: 20, totalCount: data.length, totalPages: data.length ? 1 : 0,
      hasPrevious: false, hasNext: false, ...extra };
  }
  function flushList(data: UserDto[] = [user]): void {
    http.expectOne('/api/users?page=1&pageSize=20').flush(result(data)); fixture.detectChanges();
  }
  beforeEach(() => {
    allowed = true;
    TestBed.configureTestingModule({ imports: [UsersPage], providers: [
      provideHttpClient(withXhr()), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: '/api' },
      { provide: AuthService, useValue: { canManageUsers: () => allowed } },
    ] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(UsersPage); component = fixture.componentInstance;
    fixture.detectChanges(); flushList();
  });
  afterEach(() => http.verify());

  it('uses generated Tailwind utilities for the administrative form toolbar', () => {
    const toolbar = fixture.nativeElement.querySelector('.form-actions') as HTMLElement;
    const style = getComputedStyle(toolbar);
    expect(style.display).toBe('flex');
    expect(style.alignItems).toBe('center');
    expect(style.justifyContent).toBe('flex-end');
    expect(style.columnGap).toBe('9.6px');
  });

  it('lists the name, login, role, state, creation and last access without password fields', () => {
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Academic Account'); expect(text).toContain('account');
    expect(text).toContain('Operador'); expect(text).toContain('Activo');
    expect(text).toContain('2026'); expect(text).toContain('Sin accesos');
    expect(fixture.nativeElement.querySelectorAll('table th').length).toBe(6);
    expect(text).not.toContain('PasswordHash');
    expect(fixture.nativeElement.querySelector('input[type=password]')?.autocomplete).toBe('new-password');
  });

  it('creates a user with the selected role and clears the password from the form', () => {
    component.name = 'New Account'; component.username = 'new-login'; component.password = 'Academic-Test-123'; component.role = 'Query';
    component.save();
    const request = http.expectOne('/api/users');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'New Account', username: 'new-login', password: 'Academic-Test-123', role: 'Query' });
    expect(component.password).toBe('');
    request.flush({ ...user, id: 'new-id', role: 'Query' }); flushList();
    expect(component.success).toContain('Usuario creado'); expect(component.name).toBe('');
  });

  it('edits name and login while keeping the password and role out of the update', () => {
    (fixture.nativeElement.querySelector('.edit-user') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('input[type=password]')).toBeNull();
    component.name = 'Edited Account'; component.username = 'updated-login'; component.save();
    const request = http.expectOne('/api/users/account-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Edited Account', username: 'updated-login' });
    request.flush({ ...user, name: component.name, username: component.username }); flushList();
    expect(component.editingId).toBe(''); expect(component.success).toContain('actualizado');
  });

  it('applies search, role and inactive filters and clears them', () => {
    component.draftFilters = { search: ' account ', role: 'Query', isActive: 'false' };
    component.applyFilters();
    const request = http.expectOne('/api/users?page=1&pageSize=20&search=account&role=Query&isActive=false');
    request.flush(result([])); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('No se encontraron usuarios');
    component.clearFilters(); flushList(); expect(component.activeFilters.search).toBe('');
  });

  it('deactivates and reactivates accounts after confirmation', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    for (const active of [false, true]) {
      component.toggleStatus({ ...user, isActive: !active });
      const request = http.expectOne('/api/users/account-1/status');
      expect(request.request.method).toBe('PATCH'); expect(request.request.body).toEqual({ isActive: active });
      request.flush({ ...user, isActive: active }); flushList([{ ...user, isActive: active }]);
      expect(fixture.nativeElement.textContent).toContain(active ? 'Activo' : 'Inactivo');
    }
  });

  it('changes a role explicitly after confirmation', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    component.roleDraft[user.id] = 'Administrator'; component.assignRole(user);
    const request = http.expectOne('/api/users/account-1/role');
    expect(request.request.method).toBe('PATCH'); expect(request.request.body).toEqual({ role: 'Administrator' });
    request.flush({ ...user, role: 'Administrator' }); flushList([{ ...user, role: 'Administrator' }]);
    expect(component.success).toContain('Rol actualizado');
  });

  it('does not issue writes after permission is lost', () => {
    allowed = false; fixture.detectChanges();
    component.save(); component.edit(user); component.toggleStatus(user); component.assignRole(user); component.load();
    http.expectNone(request => request.url.startsWith('/api/users'));
    expect(fixture.nativeElement.querySelector('form')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Solo un administrador');
  });

  it('shows duplicate-login errors without disclosing the submitted password', () => {
    component.name = 'Duplicate'; component.username = 'account'; component.password = 'Academic-Test-123'; component.save();
    http.expectOne('/api/users').flush({ detail: 'El login ya está registrado.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges(); expect(component.error).toBe('El login ya está registrado.');
    expect(component.saving).toBeFalse(); expect(component.password).toBe('');
    expect(fixture.nativeElement.textContent).not.toContain('Academic-Test-123');
  });

  it('paginates without discarding the active filters', () => {
    component.activeFilters = { search: 'account', role: '', isActive: '' }; component.hasNext = true; component.next();
    http.expectOne('/api/users?page=2&pageSize=20&search=account').flush(result([user], { pageIndex: 2, hasPrevious: true }));
    expect(component.page).toBe(2); component.previous();
    http.expectOne('/api/users?page=1&pageSize=20&search=account').flush(result());
    expect(component.page).toBe(1);
  });

  it('cancels confirmation without issuing writes', () => {
    const confirm = spyOn(window, 'confirm').and.returnValue(false);
    component.toggleStatus(user); component.roleDraft[user.id] = 'Administrator'; component.assignRole(user);
    http.expectNone(request => request.method === 'PATCH');
    expect(confirm).toHaveBeenCalledTimes(2);
  });

  it('validates required fields and minimum password length before sending', () => {
    component.save(); expect(component.error).toContain('Completa nombre');
    component.name = 'Account'; component.username = 'valid-login'; component.password = 'short'; component.save();
    expect(component.error).toContain('8 y 128'); http.expectNone('/api/users');
  });

  for (const status of [401, 403]) {
    it(`explains a ${status} response from the backend`, () => {
      component.load(); http.expectOne('/api/users?page=1&pageSize=20').flush({}, { status, statusText: 'Denied' });
      expect(component.error).toContain(status === 401 ? 'Inicia sesión' : 'Solo un administrador');
    });
  }

  it('retrieves a single user through the typed API', () => {
    TestBed.inject(UserApiService).getById(user.id).subscribe(value => expect(value).toEqual(user));
    http.expectOne('/api/users/account-1').flush(user);
  });
});
