import { Component, DestroyRef, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { RiskEventDetailDto } from '../../../../core/models/event.model';
import { EventApiService } from '../../../../core/services/event-api.service';
import { alertStatusLabels, conditionLabel, eventStatusLabels, formatEventDate, levelLabels, phenomenonLabels } from '../../event-presentation';

@Component({ selector: 'app-event-detail-page', imports: [RouterLink], templateUrl: './event-detail-page.html', changeDetection: ChangeDetectionStrategy.Eager,
 styleUrl: '../events-page/events-page.scss' })
export class EventDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(EventApiService);
  private readonly destroyRef = inject(DestroyRef);
  detail: RiskEventDetailDto | null = null;
  loading = true; error = '';
  readonly phenomenonLabels = phenomenonLabels; readonly levelLabels = levelLabels;
  readonly statusLabels = eventStatusLabels; readonly alertStatusLabels = alertStatusLabels;
  readonly formatDate = formatEventDate; readonly condition = conditionLabel;

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => {
      this.loading = true; this.error = ''; this.detail = null;
      return this.api.getById(params.get('id')!);
    }), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => { this.detail = data; this.loading = false; },
      error: response => {
        this.loading = false;
        this.error = response.status === 404 ? 'El evento no existe.'
          : response.status === 401 ? 'Inicia sesión para consultar el evento.'
          : response.status === 403 ? 'Tu cuenta no tiene permiso para consultar eventos.'
          : 'No se pudo consultar el detalle del evento.';
      },
    });
  }
}
