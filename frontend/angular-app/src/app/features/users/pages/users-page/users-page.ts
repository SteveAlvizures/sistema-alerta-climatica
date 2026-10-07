import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Observable, Subscription } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { UserApiService, UserDto, UserFilters, UserRole } from '../../../../core/services/user-api.service';

const emptyFilters = (): UserFilters => ({ search: '', role: '', isActive: '' });
const roleLabels: Record<UserRole, string> = {
  Administrator: 'Administrador', Operator: 'Operador', ConsultationUser: 'Usuario de consulta',
};

@Component({
  selector: 'app-users-page', imports: [FormsModule],
  templateUrl: './users-page.html', changeDetection: ChangeDetectionStrategy.Eager,
 styleUrl: './users-page.scss',
})
export class UsersPage implements OnInit {
  private readonly api = inject(UserApiService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private listRequest?: Subscription;
  readonly roles = Object.keys(roleLabels) as UserRole[];
  users: UserDto[] = [];
  draftFilters = emptyFilters();
  activeFilters = emptyFilters();
  roleDraft: Record<string, UserRole> = {};
  page = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 0;
  hasPrevious = false;
  hasNext = false;
  loading = false;
  saving = false;
  error = '';
  success = '';
  editingId = '';
  name = '';
  username = '';
  password = '';
  role: UserRole = 'ConsultationUser';

  ngOnInit(): void { this.load(); }
  canManage(): boolean { return this.auth.canManageUsers(); }
  roleLabel(role: UserRole): string { return roleLabels[role]; }
  formatDate(value: string | null): string {
    return value ? new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Sin accesos';
  }
  load(): void {
    if (!this.canManage()) return;
    this.listRequest?.unsubscribe();
    this.loading = true;
    this.error = '';
    this.listRequest = this.api.getPage(this.page, this.pageSize, this.activeFilters)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => {
          this.users = result.data;
          this.roleDraft = Object.fromEntries(result.data.map(user => [user.id, user.role]));
          this.page = result.pageIndex; this.totalCount = result.totalCount; this.totalPages = result.totalPages;
          this.hasPrevious = result.hasPrevious; this.hasNext = result.hasNext; this.loading = false;
        },
        error: response => { this.loading = false; this.error = this.errorMessage(response); },
      });
  }
  applyFilters(): void { this.activeFilters = { ...this.draftFilters }; this.page = 1; this.load(); }
  clearFilters(): void { this.draftFilters = emptyFilters(); this.activeFilters = emptyFilters(); this.page = 1; this.load(); }
  previous(): void { if (this.hasPrevious) { this.page--; this.load(); } }
  next(): void { if (this.hasNext) { this.page++; this.load(); } }
  changePageSize(): void { this.page = 1; this.load(); }
  edit(user: UserDto): void {
    if (!this.canManage() || this.saving) return;
    this.editingId = user.id; this.name = user.name; this.username = user.username;
    this.password = ''; this.error = ''; this.success = '';
  }
  cancelEdit(): void {
    this.editingId = ''; this.name = ''; this.username = ''; this.password = ''; this.role = 'ConsultationUser';
  }
  save(): void {
    if (!this.canManage() || this.saving) return;
    if (!this.name.trim() || !this.username.trim() || /\s/.test(this.username.trim())) {
      this.error = 'Completa nombre y login, sin espacios en el login.'; return;
    }
    if (!this.editingId && (this.password.length < 8 || this.password.length > 128 || !this.password.trim())) {
      this.error = 'La contraseña debe tener entre 8 y 128 caracteres.'; return;
    }
    const profile = { name: this.name.trim(), username: this.username.trim() };
    const editing = !!this.editingId;
    const operation = editing ? this.api.update(this.editingId, profile)
      : this.api.create({ ...profile, role: this.role, password: this.password });
    this.password = '';
    this.runMutation(operation, editing ? 'Usuario actualizado correctamente.' : 'Usuario creado correctamente.', true);
  }
  toggleStatus(user: UserDto): void {
    if (!this.canManage() || this.saving) return;
    if (!window.confirm(`¿Deseas ${user.isActive ? 'desactivar' : 'activar'} la cuenta ${user.username}?`)) return;
    this.runMutation(this.api.changeStatus(user.id, !user.isActive), user.isActive ? 'Usuario desactivado.' : 'Usuario activado.');
  }
  assignRole(user: UserDto): void {
    const role = this.roleDraft[user.id];
    if (!this.canManage() || this.saving || !role || role === user.role) return;
    if (!window.confirm(`¿Asignar el rol ${this.roleLabel(role)} a ${user.username}?`)) return;
    this.runMutation(this.api.changeRole(user.id, role), 'Rol actualizado correctamente.');
  }
  private runMutation(operation: Observable<UserDto>, message: string, resetForm = false): void {
    this.saving = true; this.error = ''; this.success = '';
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.saving = false; this.success = message;
        if (resetForm) this.cancelEdit();
        this.load();
      },
      error: response => { this.saving = false; this.error = this.errorMessage(response); },
    });
  }
  private errorMessage(response: HttpErrorResponse): string {
    if (response.status === 401) return 'La sesión venció o la cuenta está inactiva. Inicia sesión nuevamente.';
    if (response.status === 403) return 'Solo un administrador puede gestionar usuarios.';
    return response.error?.detail || 'No fue posible completar la operación. Inténtalo de nuevo.';
  }
}
