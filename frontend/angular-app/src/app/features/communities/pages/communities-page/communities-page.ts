import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, inject, OnInit, ViewChild, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommunityDto } from '../../../../core/models/api.model';
import { CommunityApiService, CreateCommunityRequest } from '../../../../core/services/community-api.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({ selector: 'app-communities-page', imports: [FormsModule], templateUrl: './communities-page.html', changeDetection: ChangeDetectionStrategy.Eager,
 styleUrl: './communities-page.scss' })
export class CommunitiesPage implements OnInit {
  @ViewChild('communityForm') private communityForm?: ElementRef<HTMLElement>;
  @ViewChild('communityName') private communityName?: ElementRef<HTMLInputElement>;
  private readonly destroyRef = inject(DestroyRef);
  private pageRequest?: Subscription;
  private readonly communityApi = inject(CommunityApiService);
  protected readonly auth = inject(AuthService);
  communities: CommunityDto[] = [];
  loading = true;
  saving = false;
  deletingId = '';
  editingId = '';
  error = '';
  success = '';
  name = '';
  location = '';
  description = '';
  municipality = ''; department = ''; country = 'Guatemala';
  latitude: number | null = null; longitude: number | null = null; isActive = true;
  search = ''; statusFilter = ''; municipalityFilter = ''; departmentFilter = '';
  page = 1; pageSize = 20; totalCount = 0; totalPages = 0;
  private appliedFilters = { search: '', isActive: '', municipality: '', department: '' };

  applyFilters(): void {
    this.appliedFilters = { search: this.search.trim(), isActive: this.statusFilter, municipality: this.municipalityFilter.trim(), department: this.departmentFilter.trim() };
    this.page = 1; this.loadCommunities();
  }
  goToPage(page: number): void { if (page < 1 || page > this.totalPages || this.loading) return; this.page = page; this.loadCommunities(); }
  changeStatus(community: CommunityDto): void {
    if (!this.canAdminister() || this.deletingId || !confirm(`¿${community.isActive ? "Desactivar" : "Activar"} la comunidad "${community.name}"? Se conservará su historial.`)) return;
    this.deletingId = community.id;
    this.communityApi.changeStatus(community.id, !community.isActive).subscribe({
      next: () => { this.deletingId = ''; this.loadCommunities(); },
      error: (response: HttpErrorResponse) => { this.deletingId = ''; this.error = response.error?.detail || 'No se pudo cambiar el estado.'; },
    });
  }

  canAdminister(): boolean { return this.auth.canOperate(); }
  ngOnInit(): void { this.loadCommunities(); }
  loadCommunities(): void {
    this.loading = true; this.error = '';
    this.pageRequest?.unsubscribe();
    this.pageRequest = this.communityApi.getPage({ page: this.page, pageSize: this.pageSize, ...this.appliedFilters }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => { this.communities = data.data; this.totalCount = data.totalCount; this.totalPages = data.totalPages; this.loading = false; },
      error: () => { this.error = 'No se pudieron cargar las comunidades.'; this.loading = false; },
    });
  }
  saveCommunity(): void {
    if (!this.canAdminister() || this.saving) return;
    this.error = ''; this.success = '';
    if (!this.name.trim() || !this.location.trim()) { this.error = 'Nombre y ubicación son obligatorios.'; return; }
    if (!this.municipality.trim() || !this.department.trim() || !this.country.trim() || this.latitude === null || this.longitude === null ||
        !Number.isFinite(this.latitude) || !Number.isFinite(this.longitude) || Math.abs(this.latitude) > 90 || Math.abs(this.longitude) > 180) {
      this.error = 'Completa municipio, departamento, país y coordenadas válidas.'; return;
    }
    const request: CreateCommunityRequest = { name: this.name.trim(), location: this.location.trim(), description: this.description.trim() || null,
      municipality: this.municipality.trim(), department: this.department.trim(), country: this.country.trim(),
      latitude: this.latitude, longitude: this.longitude, isActive: this.isActive };
    this.saving = true;
    const operation = this.editingId ? this.communityApi.update(this.editingId, request) : this.communityApi.create(request);
    operation.subscribe({
      next: (community) => {
        this.communities = this.editingId ? this.communities.map((item) => item.id === community.id ? community : item) : [...this.communities, community];
        this.success = this.editingId ? 'Comunidad actualizada correctamente.' : 'Comunidad registrada correctamente.';
        this.resetForm(); this.saving = false; this.loadCommunities();
      },
      error: (response: HttpErrorResponse) => { this.saving = false; this.error = response.error?.detail || 'No se pudo guardar la comunidad.'; },
    });
  }
  editCommunity(community: CommunityDto): void {
    if (!this.canAdminister()) return;
    this.editingId = community.id; this.name = community.name; this.location = community.location;
    this.municipality = community.municipality ?? ''; this.department = community.department ?? ''; this.country = community.country ?? '';
    this.latitude = community.latitude ?? null; this.longitude = community.longitude ?? null; this.isActive = community.isActive;
    this.description = community.description ?? ''; this.error = ''; this.success = '';
    setTimeout(() => {
      this.communityForm?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.communityName?.nativeElement.focus({ preventScroll: true });
    });
  }
  cancelEdit(): void { this.resetForm(); }
  deleteCommunity(community: CommunityDto): void {
    if (!this.canAdminister()) return;
    if (!confirm(`¿Eliminar la comunidad "${community.name}"?`)) return;
    this.error = ''; this.success = ''; this.deletingId = community.id;
    this.communityApi.delete(community.id).subscribe({
      next: () => { this.communities = this.communities.filter((item) => item.id !== community.id); this.deletingId = ''; this.success = 'Comunidad eliminada correctamente.'; },
      error: (response: HttpErrorResponse) => {
        this.deletingId = '';
        this.error = response.status === 409 ? 'No se puede eliminar la comunidad porque tiene sensores asociados.' : response.error?.detail || 'No se pudo eliminar la comunidad.';
      },
    });
  }
  private resetForm(): void { this.editingId = ''; this.name = ''; this.location = ''; this.description = ''; this.municipality = ''; this.department = ''; this.country = 'Guatemala'; this.latitude = null; this.longitude = null; this.isActive = true; }
}
