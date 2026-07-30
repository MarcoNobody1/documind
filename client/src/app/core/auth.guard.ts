import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Guards the authenticated shell (`/`). Cookie auth carries no client-readable claims, so this
 * guard cannot decide from anything already in the browser — it awaits `ensureBootstrapped()`
 * (a `GET /api/account/me`, deduped across concurrent navigations) before deciding.
 */
export const authGuard: CanActivateFn = async () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  await authService.ensureBootstrapped();

  return authService.user() !== null || router.createUrlTree(['/login']);
};
