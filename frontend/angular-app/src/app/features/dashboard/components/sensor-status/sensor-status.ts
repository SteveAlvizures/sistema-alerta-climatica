import { Component, input } from '@angular/core';
import { ClimateSensor, sensorOriginLabels } from '../../../../core/models/sensor.model';
import { StatusBadge } from '../../../../shared/components/status-badge/status-badge';

@Component({
  selector: 'app-sensor-status',
  imports: [StatusBadge],
  templateUrl: './sensor-status.html',
  styleUrl: './sensor-status.scss',
})
export class SensorStatus {
  readonly sensors = input.required<ClimateSensor[]>();
  protected readonly originLabels = sensorOriginLabels;
}
