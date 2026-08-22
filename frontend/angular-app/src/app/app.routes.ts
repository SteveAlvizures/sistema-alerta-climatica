import { Routes } from '@angular/router';

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
        path: 'alerts',
        loadComponent: () =>
          import('./features/alerts/pages/alerts-page/alerts-page').then(
            (component) => component.AlertsPage,
          ),
      },
      {
        path: 'history',
        loadComponent: () =>
          import('./features/history/pages/history-page/history-page').then(
            (component) => component.HistoryPage,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
