import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import {
  CreateSensorRequest,
  SensorApiService,
} from '../../../../core/services/sensor-api.service';
import { CommunityDto, SensorDto } from '../../../../core/models/api.model';

@Component({
  selector: 'app-sensors-page',
  imports: [FormsModule],
  templateUrl: './sensors-page.html',
  styleUrl: './sensors-page.scss',
})
export class SensorsPage implements OnInit {
  private readonly sensorApi = inject(SensorApiService);
  private readonly communityApi = inject(CommunityApiService);

  communities: CommunityDto[] = [];
  sensors: SensorDto[] = [];

  selectedCommunityId = '';

  loading = false;
  saving = false;

  error = '';
  success = '';

  code = '';
  name = '';
  measurementType = 'Temperature';
  origin = 'Simulated';
  location = '';
  deviceCode = '';

  ngOnInit(): void {
    this.loadCommunities();
  }

  loadCommunities(): void {
    this.communityApi.getAll().subscribe({
      next: (data) => {
        this.communities = data;

        if (data.length > 0) {
          this.selectedCommunityId = data[0].id;
          this.loadSensors();
        }
      },
      error: () => {
        this.error = 'No se pudieron cargar las comunidades.';
      },
    });
  }

  loadSensors(): void {
    if (!this.selectedCommunityId) {
      this.sensors = [];
      return;
    }

    this.loading = true;
    this.error = '';

    this.sensorApi.getByCommunity(this.selectedCommunityId).subscribe({
      next: (data) => {
        this.sensors = data;
        this.loading = false;
      },
      error: () => {
        this.error = 'No se pudieron cargar los sensores.';
        this.loading = false;
      },
    });
  }

  createSensor(): void {
    this.error = '';
    this.success = '';

    if (
      !this.selectedCommunityId ||
      !this.code.trim() ||
      !this.name.trim() ||
      !this.location.trim()
    ) {
      this.error =
        'Comunidad, código, nombre y ubicación son obligatorios.';
      return;
    }

    const request: CreateSensorRequest = {
      communityId: this.selectedCommunityId,
      code: this.code.trim(),
      name: this.name.trim(),
      measurementType: this.measurementType,
      origin: this.origin,
      location: this.location.trim(),
      deviceCode: this.deviceCode.trim() || null,
    };

    this.saving = true;

    this.sensorApi.create(request).subscribe({
      next: (sensor) => {
        this.sensors = [...this.sensors, sensor];

        this.code = '';
        this.name = '';
        this.location = '';
        this.deviceCode = '';

        this.saving = false;
        this.success = 'Sensor registrado correctamente.';
      },
      error: () => {
        this.saving = false;
        this.error = 'No se pudo registrar el sensor.';
      },
    });
  }

  changeStatus(sensor: SensorDto): void {
    this.error = '';
    this.success = '';

    const isActive = sensor.status !== 'Active';

    this.sensorApi.changeStatus(sensor.id, isActive).subscribe({
      next: (updatedSensor) => {
        this.sensors = this.sensors.map((item) =>
          item.id === updatedSensor.id ? updatedSensor : item,
        );

        this.success = isActive
          ? 'Sensor activado correctamente.'
          : 'Sensor desactivado correctamente.';
      },
      error: () => {
        this.error = 'No se pudo cambiar el estado del sensor.';
      },
    });
  }
}
