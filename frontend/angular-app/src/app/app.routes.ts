import { Routes } from '@angular/router';
import { administratorGuard } from './core/guards/administrator.guard';
import { authenticatedGuard } from './core/guards/authenticated.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/pages/login-page/login-page').then(
        (component) => component.LoginPage,
      ),
  },
  {
    path: '',
    loadComponent: () =>
      import('./layout/main-layout/main-layout').then(
        (component) => component.MainLayout,
      ),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/dashboard/pages/dashboard-page/dashboard-page').then(
            (component) => component.DashboardPage,
          ),
      },
      {
        path: 'communities',
        loadComponent: () =>
          import(
            './features/communities/pages/communities-page/communities-page'
          ).then((component) => component.CommunitiesPage),
      },
      {
        path: 'sensors',
        loadComponent: () =>
          import('./features/sensors/pages/sensors-page/sensors-page').then(
            (component) => component.SensorsPage,
          ),
      },
      {
        path: 'sensors/:id',
        loadComponent: () => import('./features/sensors/pages/sensor-detail-page/sensor-detail-page').then(
          (component) => component.SensorDetailPage),
      },
      {
        path: 'alerts',
        loadComponent: () =>
          import('./features/alerts/pages/alerts-page/alerts-page').then(
            (component) => component.AlertsPage,
          ),
      },
      {
        path: 'alerts/:id',
        loadComponent: () => import('./features/alerts/pages/alert-detail-page/alert-detail-page').then(component => component.AlertDetailPage),
      },
      {
        path: 'history',
        loadComponent: () =>
          import('./features/history/pages/history-page/history-page').then(
            (component) => component.HistoryPage,
          ),
      },
      {
        path: 'events', canActivate: [authenticatedGuard],
        loadComponent: () => import('./features/events/pages/events-page/events-page').then(component => component.EventsPage),
      },
      {
        path: 'events/:id', canActivate: [authenticatedGuard],
        loadComponent: () => import('./features/events/pages/event-detail-page/event-detail-page').then(component => component.EventDetailPage),
      },
      {
        path: 'alert-rules',
        loadComponent: () => import('./features/alert-rules/pages/alert-rules-page/alert-rules-page').then((component) => component.AlertRulesPage),
      },
      {
        path: 'users',
        canActivate: [administratorGuard],
        loadComponent: () => import('./features/users/pages/users-page/users-page').then((component) => component.UsersPage),
      },
      {
        path: 'audit-log',
        canActivate: [administratorGuard],
        loadComponent: () => import('./features/audit-log/pages/audit-log-page/audit-log-page').then((component) => component.AuditLogPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
