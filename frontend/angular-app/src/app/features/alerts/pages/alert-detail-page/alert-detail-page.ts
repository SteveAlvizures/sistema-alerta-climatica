import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { AlertDto } from '../../../../core/models/api.model';
import { AlertApiService } from '../../../../core/services/alert-api.service';
import { AlertActions } from '../../components/alert-actions/alert-actions';
import { alertStatusLabels, conditionLabel, formatAlertDate, levelLabels, phenomenonLabels, variableLabels } from '../../alert-presentation';

@Component({ selector: 'app-alert-detail-page', imports: [RouterLink, AlertActions], templateUrl: './alert-detail-page.html', styleUrl: './alert-detail-page.scss' })
export class AlertDetailPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(AlertApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly alert = signal<AlertDto | null>(null);
  readonly error = signal('');
  readonly loading = signal(true);
  readonly statusLabel = (alert: AlertDto) => alertStatusLabels[alert.status];
  readonly conditionLabel = conditionLabel;
  readonly formatDate = formatAlertDate;
  readonly levelLabels = levelLabels;
  readonly phenomenonLabels = phenomenonLabels;
  readonly variableLabels = variableLabels;

  ngOnInit(): void {
    this.route.paramMap.pipe(switchMap(params => {
      this.loading.set(true); this.error.set(''); this.alert.set(null);
      return this.api.getById(params.get('id')!);
    }), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: alert => { this.alert.set(alert); this.loading.set(false); },
      error: response => { this.error.set(response.status === 404 ? 'La alerta no existe.' : 'No fue posible cargar el detalle de la alerta.'); this.loading.set(false); },
    });
  }
}
