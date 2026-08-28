import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ElementRef, ViewChild } from '@angular/core';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import {
  CreateSensorRequest,
  SensorApiService,
  UpdateSensorRequest,
} from '../../../../core/services/sensor-api.service';
import { CommunityDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-sensors-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './sensors-page.html',
  styleUrl: './sensors-page.scss',
})
export class SensorsPage implements OnInit {
  @ViewChild('editLocationInput') private editLocationInput?: ElementRef<HTMLInputElement>;
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

  measurementType = 'Temperature';
  location = '';
  initialActive = true;
  editingSensor: SensorDto | null = null;
  editLocation = '';
  readingSensor: SensorDto | null = null;
  manualValue: number | null = null;

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
      !this.location.trim()
    ) {
      this.error = 'Comunidad y ubicación o referencia son obligatorias.';
      return;
    }

    const request: CreateSensorRequest = {
      communityId: this.selectedCommunityId,
      measurementType: this.measurementType,
      location: this.location.trim(),
      isActive: this.initialActive,
    };

    this.saving = true;

    this.sensorApi.create(request).subscribe({
      next: (sensor) => {
        this.sensors = [...this.sensors, sensor];

        this.location = '';

        this.saving = false;
        this.success = `Sensor creado correctamente: ${sensor.name} (${sensor.code}).`;
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
  unitFor(value: string): string { return ({ Temperature: '°C', RelativeHumidity: '%', WindSpeed: 'km/h', RainfallLevel: 'mm', RiverOrReservoirLevel: 'm' } as Record<string, string>)[value] ?? ''; }
  generatedName(): string { return this.location.trim() ? `Sensor de ${this.variableLabel(this.measurementType).toLowerCase()} - ${this.location.trim()}` : 'Completa la ubicación o referencia'; }
  estimatedCode(): string {
    const community = this.communities.find((item) => item.id === this.selectedCommunityId);
    const communities: Record<string, string> = { 'Lanquín': 'LAN', Livingston: 'LIV', 'San Juan La Laguna': 'SJL', 'Santa Catarina Palopó': 'SCP', 'San Juan Chamelco': 'SJC', 'Todos Santos Cuchumatán': 'TSC' };
    const variables: Record<string, string> = { Temperature: 'TEMP', RelativeHumidity: 'HUM', WindSpeed: 'WIND', RainfallLevel: 'RAIN', RiverOrReservoirLevel: 'RIVER' };
    return community ? `SEN-${communities[community.name] ?? 'COM'}-${variables[this.measurementType]}-XX` : 'Selecciona una comunidad';
  }
  communityName(sensor: SensorDto): string { return this.communities.find((item) => item.id === sensor.communityId)?.name ?? 'Comunidad'; }

  startEdit(sensor: SensorDto): void {
    if (!this.canAdminister()) return;
    this.editingSensor = sensor;
    this.editLocation = sensor.location;
    this.readingSensor = null;
    setTimeout(() => {
      this.editLocationInput?.nativeElement.focus();
      this.editLocationInput?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  cancelEdit(): void { this.editingSensor = null; }

  saveEdit(): void {
    if (!this.editingSensor || !this.editLocation.trim()) return;
    const request: UpdateSensorRequest = { location: this.editLocation.trim() };
    this.saving = true; this.error = ''; this.success = '';
    this.sensorApi.update(this.editingSensor.id, request).subscribe({
      next: (updated) => { this.sensors = this.sensors.map((item) => item.id === updated.id ? updated : item); this.editingSensor = null; this.saving = false; this.success = 'Sensor actualizado correctamente.'; },
      error: () => { this.saving = false; this.error = 'No se pudo actualizar el sensor.'; },
    });
  }

  startManualReading(sensor: SensorDto): void {
    if (!this.canAdminister()) return;
    if (sensor.status !== 'Active') {
      this.error = 'El sensor está inactivo. Actívalo antes de registrar una lectura.';
      return;
    }
    this.readingSensor = sensor; this.manualValue = null; this.editingSensor = null; this.error = ''; this.success = '';
  }

  cancelManualReading(): void { this.readingSensor = null; this.manualValue = null; }

  registerManualReading(): void {
    if (!this.readingSensor || this.manualValue === null || !Number.isFinite(this.manualValue)) {
      this.error = 'Ingresa un valor numérico válido.'; return;
    }
    this.saving = true; this.error = ''; this.success = '';
    this.sensorReadingApi.createManual(this.readingSensor.id, this.manualValue).subscribe({
      next: (reading) => {
        this.latestReadings[reading.sensorId] = reading;
        this.readingSensor = null; this.manualValue = null; this.saving = false;
        this.success = 'Lectura registrada correctamente.';
      },
      error: (response) => {
        this.saving = false;
        this.error = response.status === 409
          ? 'El sensor está inactivo. Actívalo antes de registrar una lectura.'
          : 'No se pudo registrar la lectura.';
      },
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
