import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { DashboardDataService } from '../../core/services/dashboard-data.service';
import { AuthService } from '../../core/services/auth.service';

interface NavigationItem {
  label: string;
  symbol: string;
  route: string | null;
  upcoming: boolean;
  administratorOnly?: boolean;
}

@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.html',
  styleUrl: './sidebar.scss',
})
export class Sidebar {
  protected readonly climate = inject(DashboardDataService);
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly navigation: NavigationItem[] = [
    {
      label: 'Resumen',
      symbol: '⌂',
      route: '/',
      upcoming: false,
    },
    {
      label: 'Sensores',
      symbol: '⌁',
      route: '/sensors',
      upcoming: false,
    },
    {
      label: 'Alertas',
      symbol: '!',
      route: '/alerts',
      upcoming: false,
    },
    {
      label: 'Historial',
      symbol: '↻',
      route: '/history',
      upcoming: false,
    },
    { label: 'Reglas', symbol: '⚙', route: '/alert-rules', upcoming: false },
    {
      label: 'Comunidades',
      symbol: '◇',
      route: '/communities',
      upcoming: false,
    },
    { label: 'Bitácora', symbol: '☷', route: '/audit-log', upcoming: false, administratorOnly: true },
  ];

  protected get visibleNavigation(): NavigationItem[] {
    return this.navigation.filter((item) => !item.administratorOnly || this.auth.session()?.role === 'Administrator');
  }

  protected navigate(item: NavigationItem): void {
    if (!item.route || item.upcoming) {
      return;
    }

    void this.router.navigateByUrl(item.route);
  }

  protected isActive(item: NavigationItem): boolean {
    if (!item.route) {
      return false;
    }

    if (item.route === '/') {
      return this.router.url === '/';
    }

    return this.router.url.startsWith(item.route);
  }
}
