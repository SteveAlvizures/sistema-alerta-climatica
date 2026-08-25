import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ClimateVariable, CommunityDto, SensorDto, SensorReadingDto } from '../../../../core/models/api.model';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';

interface HistoryItem { reading: SensorReadingDto; sensor: SensorDto; community: CommunityDto; }
const variableLabels: Record<ClimateVariable, string> = { Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', WindSpeed: 'Velocidad del viento', RainfallLevel: 'Nivel de lluvia', RiverOrReservoirLevel: 'Nivel de río o reservorio' };

@Component({ selector: 'app-history-page', imports: [FormsModule], templateUrl: './history-page.html', styleUrl: './history-page.scss' })
export class HistoryPage implements OnInit {
  private readonly communitiesApi = inject(CommunityApiService);
  private readonly sensorsApi = inject(SensorApiService);
  private readonly readingsApi = inject(SensorReadingApiService);
  protected communities: CommunityDto[] = [];
  protected sensors: SensorDto[] = [];
  protected items: HistoryItem[] = [];
  protected selectedCommunityId = '';
  protected selectedSensorId = '';
  protected selectedVariable = '';
  protected dateFrom = '';
  protected dateTo = '';
  protected loading = true;
  protected error = '';
  protected pageIndex = 1;
  protected pageSize = 20;
  protected totalPages = 0;
  protected totalCount = 0;
  protected hasPrevious = false;
  protected hasNext = false;
  protected readonly pageSizes = [10, 20, 50];
  protected readonly variables = Object.keys(variableLabels) as ClimateVariable[];

  ngOnInit(): void { this.loadCommunities(); }
  protected get filteredItems(): HistoryItem[] {
    const from = this.dateFrom ? new Date(`${this.dateFrom}T00:00:00`).getTime() : -Infinity;
    const to = this.dateTo ? new Date(`${this.dateTo}T23:59:59.999`).getTime() : Infinity;
    return this.items.filter(({ reading }) => (!this.selectedVariable || reading.variable === this.selectedVariable) && Date.parse(reading.measuredAt) >= from && Date.parse(reading.measuredAt) <= to);
  }
  protected get selectedCommunity(): CommunityDto | undefined { return this.communities.find((item) => item.id === this.selectedCommunityId); }
  protected get lastReading(): HistoryItem | undefined { return this.filteredItems[0]; }
  protected get variableSummary(): string { return this.selectedVariable ? this.variableLabel(this.selectedVariable as ClimateVariable) : 'Todas'; }
  protected get periodSummary(): string { return !this.dateFrom && !this.dateTo ? `Página ${this.pageIndex}` : `${this.dateFrom || 'Inicio'} – ${this.dateTo || 'Hoy'}`; }
  protected variableLabel(variable: ClimateVariable): string { return variableLabels[variable]; }
  protected originLabel(origin: string): string { return origin === 'Simulated' ? 'Simulado' : 'Físico'; }
  protected formatDate(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  protected onCommunityChange(): void { this.selectedSensorId = ''; this.selectedVariable = ''; this.pageIndex = 1; this.loadCommunitySensors(); }
  protected onSensorChange(): void { this.pageIndex = 1; this.loadHistory(); }
  protected onPageSizeChange(): void { this.pageIndex = 1; this.loadHistory(); }
  protected previousPage(): void { if (this.hasPrevious) { this.pageIndex--; this.loadHistory(); } }
  protected nextPage(): void { if (this.hasNext) { this.pageIndex++; this.loadHistory(); } }
  protected clearFilters(): void { this.selectedVariable = ''; this.dateFrom = ''; this.dateTo = ''; }

  private loadCommunities(): void {
    this.loading = true; this.error = '';
    this.communitiesApi.getAll().subscribe({
      next: (communities) => { this.communities = communities; if (communities.length) { this.selectedCommunityId = communities[0].id; this.loadCommunitySensors(); } else { this.loading = false; } },
      error: () => { this.error = 'No fue posible cargar las comunidades.'; this.loading = false; },
    });
  }
  private loadCommunitySensors(): void {
    if (!this.selectedCommunityId) { this.sensors = []; this.items = []; this.loading = false; return; }
    this.loading = true; this.error = '';
    this.sensorsApi.getByCommunity(this.selectedCommunityId).subscribe({
      next: (sensors) => {
        this.sensors = sensors;
        if (!sensors.length) { this.items = []; this.totalCount = 0; this.totalPages = 0; this.loading = false; return; }
        this.selectedSensorId = sensors[0].id;
        this.loadHistory();
      },
      error: () => { this.error = 'No fue posible cargar los sensores de la comunidad.'; this.loading = false; },
    });
  }
  private loadHistory(): void {
    const sensor = this.sensors.find((item) => item.id === this.selectedSensorId);
    const community = this.selectedCommunity;
    if (!sensor || !community) { this.items = []; this.loading = false; return; }
    this.loading = true; this.error = '';
    this.readingsApi.getHistory(sensor.id, this.pageIndex, this.pageSize).subscribe({
      next: (result) => {
        this.items = result.data.map((reading) => ({ reading, sensor, community }));
        this.pageIndex = result.pageIndex; this.pageSize = result.pageSize; this.totalPages = result.totalPages; this.totalCount = result.totalCount;
        this.hasPrevious = result.hasPrevious; this.hasNext = result.hasNext; this.loading = false;
      },
      error: () => { this.error = 'No fue posible cargar el historial de lecturas.'; this.loading = false; },
    });
  }
}
