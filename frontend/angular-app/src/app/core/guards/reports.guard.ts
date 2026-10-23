import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
/** Navigation hint only: Report also authorizes every request. Do not send a signed-in reader back to login. */
export const reportsGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.canOperate() || inject(Router).createUrlTree([auth.isAuthenticated() ? '/' : '/login']);
};
