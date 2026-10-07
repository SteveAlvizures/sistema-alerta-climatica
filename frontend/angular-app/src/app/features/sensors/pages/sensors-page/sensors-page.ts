import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ElementRef, ViewChild } from '@angular/core';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import {
  CreateSensorRequest,
  SensorApiService,
  UpdateSensorRequest,
} from '../../../../core/services/sensor-api.service';
import { CommunityDto, SensorDto, SensorReadingDto, SensorType } from '../../../../core/models/api.model';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';
import { AuthService } from '../../../../core/services/auth.service';
import { sensorTypes } from '../../../../core/models/sensor-types';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-sensors-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './sensors-page.html',
  styleUrl: './sensors-page.scss',
})
export class SensorsPage implements OnInit {
  @ViewChild('editLocationInput') private editLocationInput?: ElementRef<HTMLInputElement>;
  private readonly destroyRef = inject(DestroyRef);
  private pageRequest?: Subscription;
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

  readonly types = sensorTypes;
  sensorType: SensorType = 'Temperature'; name = ''; code = ''; unit = '';
  installationDate = new Date().toISOString().slice(0, 10); description = '';
  editName = ''; editCode = ''; editCommunityId = ''; editType: SensorType = 'Temperature';
  editUnit = ''; editInstallationDate = ''; editDescription = ''; editActive = true;
  typeFilter = ''; statusFilter = ''; codeFilter = ''; search = '';
  page = 1; pageSize = 20; totalCount = 0; totalPages = 0;
  private appliedFilters = { communityId: '', type: '', isActive: '', code: '', search: '' };
  applyFilters(): void { this.page = 1; this.appliedFilters = { communityId: this.selectedCommunityId, type: this.typeFilter, isActive: this.statusFilter, code: this.codeFilter.trim(), search: this.search.trim() }; this.fetchPage(); }
  goToPage(page: number): void { if (this.loading || page < 1 || page > this.totalPages) return; this.page = page; this.fetchPage(); }
  typeLabel(sensor: SensorDto): string { return this.types.find(t => t.value === sensor.type)?.label ?? this.variableLabel(sensor.measurementType); }
  selectType(): void { const type = this.types.find(t => t.value === this.sensorType)!; this.measurementType = type.variable; this.unit = type.unit; }
  selectEditType(): void { this.editUnit = this.types.find(t => t.value === this.editType)!.unit; }
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

  loadSensors(): void { this.applyFilters(); }
  private fetchPage(): void {
    this.loading = true; this.error = '';
    this.pageRequest?.unsubscribe();
    this.pageRequest = this.sensorApi.getPage({ page: this.page, pageSize: this.pageSize, ...this.appliedFilters }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => { this.sensors = data.data; this.totalCount = data.totalCount; this.totalPages = data.totalPages; this.loadLatestReadings(); this.loading = false; },
      error: () => { this.error = 'No se pudieron cargar los sensores.'; this.loading = false; },
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
    if (!this.canAdminister() || this.saving) return;
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
      name: this.name.trim() || undefined, code: this.code.trim() || undefined,
      type: this.sensorType, unit: this.unit.trim() || this.unitFor(this.measurementType),
      installationDate: this.installationDate, description: this.description.trim(),
    };

    if (!this.installationDate) { this.error = 'La fecha de instalacion es obligatoria.'; return; }
    this.saving = true;

    this.sensorApi.create(request).subscribe({
      next: (sensor) => {
        this.sensors = [...this.sensors, sensor];

        this.location = ''; this.name = ''; this.code = ''; this.description = ''; this.fetchPage();

        this.saving = false;
        this.success = `Sensor creado correctamente: ${sensor.name} (${sensor.code}).`;
      },
      error: (response) => {
        this.saving = false;
        this.error = response.error?.detail || 'No se pudo registrar el sensor.';
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
        this.fetchPage();

        this.success = isActive
          ? 'Sensor activado correctamente.'
          : 'Sensor desactivado correctamente.';
      },
      error: () => {
        this.error = 'No se pudo cambiar el estado del sensor.';
      },
    });
  }

  canAdminister(): boolean { return this.auth.canOperate(); }
  variableLabel(value: string): string { return ({ Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', WindSpeed: 'Velocidad del viento', RainfallLevel: 'Nivel de lluvia', RiverOrReservoirLevel: 'Nivel de río o reservorio' } as Record<string, string>)[value] ?? value; }
  unitFor(value: string): string { return ({ Temperature: '°C', RelativeHumidity: '%', WindSpeed: 'km/h', RainfallLevel: 'mm', SmokeConcentration: 'ppm', OtherEnvironmental: 'u', RiverOrReservoirLevel: 'm' } as Record<string, string>)[value] ?? ''; }
  generatedName(): string { return this.location.trim() ? `Sensor de ${this.variableLabel(this.measurementType).toLowerCase()} - ${this.location.trim()}` : 'Completa la ubicación o referencia'; }
  estimatedCode(): string {
    const community = this.communities.find((item) => item.id === this.selectedCommunityId);
    const communities: Record<string, string> = { 'Lanquín': 'LAN', Livingston: 'LIV', 'San Juan La Laguna': 'SJL', 'Santa Catarina Palopó': 'SCP', 'San Juan Chamelco': 'SJC', 'Todos Santos Cuchumatán': 'TSC' };
    const types: Record<SensorType, string> = { Temperature: 'TEMP', Humidity: 'HUM', WindSpeed: 'WIND', Rainfall: 'RAIN', RiverLevel: 'RIVER', ReservoirLevel: 'RIVER', SmokeFire: 'SMOKE', OtherEnvironmental: 'ENV' };
    return community ? `SEN-${communities[community.name] ?? 'COM'}-${types[this.sensorType] ?? 'VAR'}-XX` : 'Selecciona una comunidad';
  }
  communityName(sensor: SensorDto): string { return this.communities.find((item) => item.id === sensor.communityId)?.name ?? 'Comunidad'; }

  startEdit(sensor: SensorDto): void {
    if (!this.canAdminister()) return;
    this.editingSensor = sensor;
    this.editLocation = sensor.location;
    this.editName = sensor.name; this.editCode = sensor.code; this.editCommunityId = sensor.communityId;
    this.editType = sensor.type ?? this.types.find(t => t.variable === sensor.measurementType)!.value;
    this.editUnit = sensor.unit ?? this.unitFor(sensor.measurementType); this.editInstallationDate = sensor.installationDate ?? '';
    this.editDescription = sensor.description ?? ''; this.editActive = sensor.status === 'Active';
    this.readingSensor = null;
    setTimeout(() => {
      this.editLocationInput?.nativeElement.focus();
      this.editLocationInput?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  cancelEdit(): void { this.editingSensor = null; }

  saveEdit(): void {
    if (!this.canAdminister()) return;
    if (!this.editingSensor || !this.editLocation.trim()) return;
    if (!this.editName.trim() || !this.editCode.trim() || !this.editCommunityId || !this.editUnit.trim() || !this.editInstallationDate) { this.error = 'Completa los datos administrativos del sensor.'; return; }
    const request: UpdateSensorRequest = { location: this.editLocation.trim(), name: this.editName.trim(), code: this.editCode.trim(),
      communityId: this.editCommunityId, type: this.editType, unit: this.editUnit.trim(), installationDate: this.editInstallationDate,
      description: this.editDescription.trim(), isActive: this.editActive };
    this.saving = true; this.error = ''; this.success = '';
    this.sensorApi.update(this.editingSensor.id, request).subscribe({
      next: (updated) => { this.sensors = this.sensors.map((item) => item.id === updated.id ? updated : item); this.editingSensor = null; this.saving = false; this.success = 'Sensor actualizado correctamente.'; this.fetchPage(); },
      error: (response) => { this.saving = false; this.error = response.error?.detail || 'No se pudo actualizar el sensor.'; },
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
    if (!this.canAdminister()) return;
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
