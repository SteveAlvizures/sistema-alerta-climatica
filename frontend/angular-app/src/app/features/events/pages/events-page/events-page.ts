import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { CommunityDto, PagedResponse } from '../../../../core/models/api.model';
import { EventFilters, EventStatisticsDto, RiskEventDto } from '../../../../core/models/event.model';
import { EventApiService } from '../../../../core/services/event-api.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { eventStatusLabels, formatEventDate, levelLabels, phenomenonLabels } from '../../event-presentation';

@Component({ selector: 'app-events-page', imports: [FormsModule, RouterLink], templateUrl: './events-page.html', styleUrl: './events-page.scss' })
export class EventsPage implements OnInit {
  private readonly api = inject(EventApiService);
  private readonly communityApi = inject(CommunityApiService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  private applied: EventFilters = {};
  communities: CommunityDto[] = [];
  result: PagedResponse<RiskEventDto> | null = null;
  statistics: EventStatisticsDto | null = null;
  from = ''; to = ''; communityId = ''; phenomenon = ''; level = ''; status = '';
  page = 1; pageSize = 20; loading = false; error = ''; communityError = '';
  readonly phenomena = Object.entries(phenomenonLabels);
  readonly levels = Object.entries(levelLabels);
  readonly phenomenonLabels = phenomenonLabels; readonly levelLabels = levelLabels;
  readonly statusLabels = eventStatusLabels; readonly formatDate = formatEventDate;

  ngOnInit(): void {
    this.communityApi.getAll().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => this.communities = data,
      error: () => this.communityError = 'No se pudieron cargar las comunidades para el filtro.',
    });
    this.load();
  }
  applyFilters(): void {
    const from = this.from ? new Date(this.from) : null; const to = this.to ? new Date(this.to) : null;
    if ((from && Number.isNaN(from.getTime())) || (to && Number.isNaN(to.getTime())) || (from && to && from > to)) {
      this.error = 'El rango de fechas no es válido.'; return;
    }
    this.applied = { from: from?.toISOString(), to: to?.toISOString(), communityId: this.communityId,
      phenomenon: this.phenomenon, level: this.level, status: this.status };
    this.page = 1; this.load();
  }
  goToPage(page: number): void {
    if (this.loading || page < 1 || page > (this.result?.totalPages ?? 0)) return;
    this.page = page; this.load();
  }
  load(): void {
    this.request?.unsubscribe(); this.loading = true; this.error = ''; this.result = null; this.statistics = null;
    this.request = forkJoin({ page: this.api.getPage(this.applied, this.page, this.pageSize), statistics: this.api.getStatistics(this.applied) })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: data => { this.result = data.page; this.statistics = data.statistics; this.loading = false; },
        error: response => {
          this.loading = false;
          this.error = response.status === 401 ? 'Inicia sesión para consultar los eventos.'
            : response.status === 403 ? 'Tu cuenta no tiene permiso para consultar eventos.'
            : response.error?.detail || 'No se pudieron consultar los eventos.';
        },
      });
  }
}
