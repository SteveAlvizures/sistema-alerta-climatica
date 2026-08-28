import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ClimateVariable, CommunityDto, HistoryReadingDto, SensorDto } from '../../../../core/models/api.model';
import { CommunityApiService } from '../../../../core/services/community-api.service';
import { SensorApiService } from '../../../../core/services/sensor-api.service';
import { SensorReadingApiService } from '../../../../core/services/sensor-reading-api.service';

const variableLabels: Record<ClimateVariable, string> = { Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', WindSpeed: 'Velocidad del viento', RainfallLevel: 'Nivel de lluvia', RiverOrReservoirLevel: 'Nivel de río o reservorio' };
@Component({ selector: 'app-history-page', imports: [FormsModule], templateUrl: './history-page.html', styleUrl: './history-page.scss' })
export class HistoryPage implements OnInit {
  private readonly communitiesApi = inject(CommunityApiService); private readonly sensorsApi = inject(SensorApiService); private readonly readingsApi = inject(SensorReadingApiService);
  protected communities: CommunityDto[] = []; protected sensors: SensorDto[] = []; protected items: HistoryReadingDto[] = [];
  protected selectedCommunityId = ''; protected selectedSensorId = ''; protected selectedVariable = ''; protected dateFrom = ''; protected dateTo = '';
  protected loading = true; protected error = ''; protected pageIndex = 1; protected pageSize = 20; protected totalPages = 0; protected totalCount = 0; protected hasPrevious = false; protected hasNext = false;
  protected readonly pageSizes = [10, 20, 50]; protected readonly variables = Object.keys(variableLabels) as ClimateVariable[];
  ngOnInit(): void { this.loading = true; forkJoin({ communities: this.communitiesApi.getAll(), sensors: this.sensorsApi.getAll() }).subscribe({ next: result => { this.communities = result.communities; this.sensors = result.sensors; this.loadHistory(); }, error: () => { this.error = 'No fue posible cargar los filtros del historial.'; this.loading = false; } }); }
  protected get availableSensors(): SensorDto[] { return this.selectedCommunityId ? this.sensors.filter(sensor => sensor.communityId === this.selectedCommunityId) : this.sensors; }
  protected get lastReading(): HistoryReadingDto | undefined { return this.items[0]; }
  protected get variableSummary(): string { return this.selectedVariable ? this.variableLabel(this.selectedVariable as ClimateVariable) : 'Todas'; }
  protected get querySummary(): string { return this.communities.find(item => item.id === this.selectedCommunityId)?.name ?? 'Todas las comunidades'; }
  protected variableLabel(variable: ClimateVariable): string { return variableLabels[variable]; }
  protected originLabel(origin: string): string { return origin === 'Simulated' ? 'Simulado' : 'Manual'; }
  protected formatDate(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }
  protected onCommunityChange(): void { this.selectedSensorId = ''; this.pageIndex = 1; this.loadHistory(); }
  protected applyFilters(): void { this.pageIndex = 1; this.loadHistory(); }
  protected onPageSizeChange(): void { this.pageIndex = 1; this.loadHistory(); }
  protected previousPage(): void { if (this.hasPrevious) { this.pageIndex--; this.loadHistory(); } }
  protected nextPage(): void { if (this.hasNext) { this.pageIndex++; this.loadHistory(); } }
  protected clearFilters(): void { this.selectedCommunityId = ''; this.selectedSensorId = ''; this.selectedVariable = ''; this.dateFrom = ''; this.dateTo = ''; this.pageIndex = 1; this.loadHistory(); }
  private loadHistory(): void { this.loading = true; this.error = ''; this.readingsApi.getHistoryPage({ page:this.pageIndex, pageSize:this.pageSize, communityId:this.selectedCommunityId || undefined, sensorId:this.selectedSensorId || undefined, variable:(this.selectedVariable || undefined) as ClimateVariable | undefined, dateFrom:this.dateFrom || undefined, dateTo:this.dateTo || undefined }).subscribe({ next: result => { this.items=result.data; this.pageIndex=result.pageIndex; this.pageSize=result.pageSize; this.totalPages=result.totalPages; this.totalCount=result.totalCount; this.hasPrevious=result.hasPrevious; this.hasNext=result.hasNext; this.loading=false; }, error:()=>{this.error='No fue posible cargar el historial de lecturas.';this.loading=false;} }); }
}
