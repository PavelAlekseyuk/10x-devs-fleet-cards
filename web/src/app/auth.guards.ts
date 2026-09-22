import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanMatchFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = auth.user() ?? (await auth.restoreSession());
  return user ? true : router.createUrlTree(['/login']);
};

export const readWriteGuard: CanMatchFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const user = auth.user() ?? (await auth.restoreSession());
  return user?.permission === 'ReadWrite' ? true : router.createUrlTree(['/overview']);
};
