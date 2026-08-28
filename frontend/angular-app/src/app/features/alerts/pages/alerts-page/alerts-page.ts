import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AlertDto, ApiDangerLevel, ClimateVariable, CommunityDto } from '../../../../core/models/api.model';
import { AlertApiService } from '../../../../core/services/alert-api.service';
import { CommunityApiService } from '../../../../core/services/community-api.service';

const levelLabels: Record<ApiDangerLevel, string> = { Green: 'Normal', Yellow: 'Preventiva', Orange: 'Alta', Red: 'Crítica' };
const variableLabels: Record<ClimateVariable, string> = { Temperature: 'Temperatura', RelativeHumidity: 'Humedad relativa', WindSpeed: 'Velocidad del viento', RainfallLevel: 'Nivel de lluvia', RiverOrReservoirLevel: 'Nivel de río o reservorio' };

@Component({ selector: 'app-alerts-page', imports: [FormsModule, RouterLink], templateUrl: './alerts-page.html', styleUrl: './alerts-page.scss' })
export class AlertsPage implements OnInit {
  private readonly alertsApi = inject(AlertApiService);
  private readonly communitiesApi = inject(CommunityApiService);
  protected communities: CommunityDto[] = [];
  protected alerts: AlertDto[] = [];
  protected communityFilter = '';
  protected variableFilter = '';
  protected levelFilter = '';
  protected applied = false;
  protected loading = false;
  protected error = '';
  protected pageIndex = 1;
  protected pageSize = 20;
  protected totalCount = 0;
  protected totalPages = 0;
  protected hasPrevious = false;
  protected hasNext = false;
  protected levelCounts: Record<string, number> = { Yellow: 0, Orange: 0, Red: 0 };
  protected readonly pageSizes = [10, 20, 50];
  protected readonly levels: ApiDangerLevel[] = ['Yellow', 'Orange', 'Red'];
  protected readonly variables: ClimateVariable[] = ['Temperature', 'RelativeHumidity', 'WindSpeed', 'RainfallLevel', 'RiverOrReservoirLevel'];

  ngOnInit(): void { this.communitiesApi.getAll().subscribe({ next: value => this.communities = value, error: () => this.error = 'No fue posible cargar las comunidades.' }); }
  protected applyFilters(): void { this.applied = true; this.pageIndex = 1; this.load(); }
  protected clearFilters(): void { this.communityFilter = ''; this.variableFilter = ''; this.levelFilter = ''; this.applied = false; this.alerts = []; this.pageIndex = 1; this.totalCount = 0; this.totalPages = 0; this.hasPrevious = false; this.hasNext = false; this.error = ''; }
  protected previous(): void { if (this.hasPrevious) { this.pageIndex--; this.load(); } }
  protected next(): void { if (this.hasNext) { this.pageIndex++; this.load(); } }
  protected changePageSize(): void { if (this.applied) { this.pageIndex = 1; this.load(); } }
  protected countLevel(level: ApiDangerLevel): number { return this.levelCounts[level] ?? 0; }
  protected get firstItem(): number { return this.totalCount ? (this.pageIndex - 1) * this.pageSize + 1 : 0; }
  protected get lastItem(): number { return Math.min(this.pageIndex * this.pageSize, this.totalCount); }
  protected communityName(id: string): string { return this.communities.find(item => item.id === id)?.name ?? 'Comunidad no disponible'; }
  protected levelLabel(level: ApiDangerLevel): string { return levelLabels[level]; }
  protected variableLabel(value: ClimateVariable): string { return variableLabels[value]; }
  protected formatDate(value: string): string { return new Intl.DateTimeFormat('es-GT', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)); }

  private load(): void {
    this.loading = true; this.error = '';
    this.alertsApi.getPage({ page: this.pageIndex, pageSize: this.pageSize, communityId: this.communityFilter || undefined,
      variable: (this.variableFilter || undefined) as ClimateVariable | undefined, level: (this.levelFilter || undefined) as ApiDangerLevel | undefined }).subscribe({
      next: response => { this.alerts = response.data; this.pageIndex = response.pageIndex; this.pageSize = response.pageSize; this.totalCount = response.totalCount; this.totalPages = response.totalPages; this.hasPrevious = response.hasPrevious; this.hasNext = response.hasNext; this.levelCounts = { Yellow: response.preventiveCount, Orange: response.highCount, Red: response.criticalCount }; this.loading = false; },
      error: () => { this.error = 'No fue posible cargar las alertas. Inténtalo de nuevo.'; this.loading = false; },
    });
  }
}
