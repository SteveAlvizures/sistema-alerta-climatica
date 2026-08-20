import { Component, inject } from '@angular/core';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
interface NavigationItem { label: string; symbol: string; active: boolean; upcoming: boolean; }
@Component({ selector: 'app-sidebar', templateUrl: './sidebar.html', styleUrl: './sidebar.scss' })
export class Sidebar {
  protected readonly climate = inject(DashboardDataService);
  protected readonly navigation: NavigationItem[] = [
    { label: 'Resumen', symbol: '⌂', active: true, upcoming: false },
    { label: 'Sensores', symbol: '⌁', active: false, upcoming: true },
    { label: 'Alertas', symbol: '!', active: false, upcoming: true },
    { label: 'Historial', symbol: '↺', active: false, upcoming: true },
    { label: 'Comunidades', symbol: '◇', active: false, upcoming: true },
  ];
}
