import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import {
  CreateSensorRequest,
  SensorApiService,
  UpdateSensorRequest,
} from '../../../../core/services/sensor-api.service';
import { CommunityDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-sensors-page',
  imports: [FormsModule],
  templateUrl: './sensors-page.html',
  styleUrl: './sensors-page.scss',
})
export class SensorsPage implements OnInit {
  private readonly sensorApi = inject(SensorApiService);
  private readonly communityApi = inject(CommunityApiService);
  private readonly sensorReadingApi = inject(SensorReadingApiService);
  protected readonly auth = inject(AuthService);

  communities: CommunityDto[] = [];
  sensors: SensorDto[] = [];
  latestReadings: Record<string, SensorReadingDto | null> = {};

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
  editingSensor: SensorDto | null = null;
  editCode = '';
  editName = '';
  editLocation = '';
  editDeviceCode = '';

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
        this.loadLatestReadings();
        this.loading = false;
      },
      error: () => {
        this.error = 'No se pudieron cargar los sensores.';
        this.loading = false;
      },
    });
  }


  loadLatestReadings(): void {
    this.sensors.forEach((sensor) => {
      this.sensorReadingApi.getLatest(sensor.id).subscribe({
        next: (reading) => {
          this.latestReadings[sensor.id] = reading;
        },
        error: () => {
          this.latestReadings[sensor.id] = null;
        },
      });
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

    if (!this.canAdminister()) return;
    const isActive = sensor.status !== 'Active';
    if (!window.confirm(`¿Deseas ${isActive ? 'activar' : 'desactivar'} este sensor?`)) return;

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

  canAdminister(): boolean { return this.auth.session()?.role === 'Administrator'; }
  variableLabel(value: string): string { return ({ Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', WindSpeed: 'Velocidad del viento', RainfallLevel: 'Nivel de lluvia', RiverOrReservoirLevel: 'Nivel de río o reservorio' } as Record<string, string>)[value] ?? value; }

  startEdit(sensor: SensorDto): void {
    if (!this.canAdminister()) return;
    this.editingSensor = sensor; this.editCode = sensor.code; this.editName = sensor.name;
    this.editLocation = sensor.location; this.editDeviceCode = sensor.deviceCode ?? '';
  }

  cancelEdit(): void { this.editingSensor = null; }

  saveEdit(): void {
    if (!this.editingSensor || !this.editCode.trim() || !this.editName.trim() || !this.editLocation.trim()) return;
    const request: UpdateSensorRequest = { code: this.editCode.trim(), name: this.editName.trim(), location: this.editLocation.trim(), deviceCode: this.editDeviceCode.trim() || null };
    this.saving = true; this.error = ''; this.success = '';
    this.sensorApi.update(this.editingSensor.id, request).subscribe({
      next: (updated) => { this.sensors = this.sensors.map((item) => item.id === updated.id ? updated : item); this.editingSensor = null; this.saving = false; this.success = 'Sensor actualizado correctamente.'; },
      error: () => { this.saving = false; this.error = 'No se pudo actualizar el sensor.'; },
    });
  }

  restartMonitoring(sensor: SensorDto): void {
    if (!this.canAdminister() || !window.confirm('¿Deseas reiniciar el monitoreo? El sensor quedará activo y conservará todo su historial.')) return;
    this.sensorApi.changeStatus(sensor.id, true).subscribe({
      next: (updated) => { this.sensors = this.sensors.map((item) => item.id === updated.id ? updated : item); this.success = 'Monitoreo reiniciado. El historial se conservó intacto.'; this.error = ''; },
      error: () => { this.error = 'No se pudo reiniciar el monitoreo.'; },
    });
  }
}
