import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
export const reportsGuard: CanActivateFn = () => inject(AuthService).canOperate() || inject(Router).createUrlTree(['/login']);
