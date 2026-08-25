import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, inject, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommunityDto } from '../../../../core/models/api.model';
import { CommunityApiService, CreateCommunityRequest } from '../../../../core/services/community-api.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({ selector: 'app-communities-page', imports: [FormsModule], templateUrl: './communities-page.html', styleUrl: './communities-page.scss' })
export class CommunitiesPage implements OnInit {
  @ViewChild('communityForm') private communityForm?: ElementRef<HTMLElement>;
  @ViewChild('communityName') private communityName?: ElementRef<HTMLInputElement>;
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

  canAdminister(): boolean { return this.auth.session()?.role === 'Administrator'; }
  ngOnInit(): void { this.loadCommunities(); }
  loadCommunities(): void {
    this.loading = true; this.error = '';
    this.communityApi.getAll().subscribe({
      next: (data) => { this.communities = data; this.loading = false; },
      error: () => { this.error = 'No se pudieron cargar las comunidades.'; this.loading = false; },
    });
  }
  saveCommunity(): void {
    this.error = ''; this.success = '';
    if (!this.name.trim() || !this.location.trim()) { this.error = 'Nombre y ubicación son obligatorios.'; return; }
    const request: CreateCommunityRequest = { name: this.name.trim(), location: this.location.trim(), description: this.description.trim() || null };
    this.saving = true;
    const operation = this.editingId ? this.communityApi.update(this.editingId, request) : this.communityApi.create(request);
    operation.subscribe({
      next: (community) => {
        this.communities = this.editingId ? this.communities.map((item) => item.id === community.id ? community : item) : [...this.communities, community];
        this.success = this.editingId ? 'Comunidad actualizada correctamente.' : 'Comunidad registrada correctamente.';
        this.resetForm(); this.saving = false;
      },
      error: (response: HttpErrorResponse) => { this.saving = false; this.error = response.error?.detail || 'No se pudo guardar la comunidad.'; },
    });
  }
  editCommunity(community: CommunityDto): void {
    this.editingId = community.id; this.name = community.name; this.location = community.location;
    this.description = community.description ?? ''; this.error = ''; this.success = '';
    setTimeout(() => {
      this.communityForm?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.communityName?.nativeElement.focus({ preventScroll: true });
    });
  }
  cancelEdit(): void { this.resetForm(); }
  deleteCommunity(community: CommunityDto): void {
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
  private resetForm(): void { this.editingId = ''; this.name = ''; this.location = ''; this.description = ''; }
}
