import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CommunityApiService,
  CreateCommunityRequest,
} from '../../../../core/services/community-api.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-communities-page',
  imports: [FormsModule],
  templateUrl: './communities-page.html',
  styleUrl: './communities-page.scss',
})
export class CommunitiesPage implements OnInit {
  private readonly communityApi = inject(CommunityApiService);
  protected readonly auth = inject(AuthService);

  communities: any[] = [];

  loading = true;
  saving = false;

  error = '';
  success = '';

  name = '';
  location = '';
  description = '';

  canAdminister(): boolean { return this.auth.session()?.role === 'Administrator'; }

  ngOnInit(): void {
    this.loadCommunities();
  }

  loadCommunities(): void {
    this.loading = true;
    this.error = '';

    this.communityApi.getAll().subscribe({
      next: (data) => {
        this.communities = data;
        this.loading = false;
      },
      error: () => {
        this.error = 'No se pudieron cargar las comunidades.';
        this.loading = false;
      },
    });
  }

  createCommunity(): void {
    this.error = '';
    this.success = '';

    if (!this.name.trim() || !this.location.trim()) {
      this.error = 'Nombre y ubicación son obligatorios.';
      return;
    }

    const request: CreateCommunityRequest = {
      name: this.name.trim(),
      location: this.location.trim(),
      description: this.description.trim() || null,
    };

    this.saving = true;

    this.communityApi.create(request).subscribe({
      next: (community) => {
        this.communities = [...this.communities, community];

        this.name = '';
        this.location = '';
        this.description = '';

        this.saving = false;
        this.success = 'Comunidad registrada correctamente.';
      },
      error: () => {
        this.saving = false;
        this.error = 'No se pudo registrar la comunidad.';
      },
    });
  }
}
