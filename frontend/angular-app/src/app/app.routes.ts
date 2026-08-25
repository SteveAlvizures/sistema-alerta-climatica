import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/login-page/login-page').then(
        (component) => component.LoginPage,
      ),
  },
  {
    path: '',
    canActivate: [authGuard],
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
    ],
  },
  { path: '**', redirectTo: '' },
];