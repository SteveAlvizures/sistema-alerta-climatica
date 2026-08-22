import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const administratorGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() && auth.session()?.role === 'Administrator'
    ? true
    : inject(Router).createUrlTree(['/login']);
};
