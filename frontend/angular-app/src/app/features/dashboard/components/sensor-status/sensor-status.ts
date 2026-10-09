import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { ClimateSensor, sensorOriginLabels } from '../../../../core/models/sensor.model';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';

@Component({
  selector: 'app-sensor-status',
  imports: [StatusBadge],
  templateUrl: './sensor-status.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './sensor-status.scss',
})
export class SensorStatus {
  readonly sensors = input.required<ClimateSensor[]>();
  protected readonly originLabels = sensorOriginLabels;
}
