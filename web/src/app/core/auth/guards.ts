import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const signedInGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return (
    auth.signedIn() ||
    inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } })
  );
};

/** Residents must finish sign-up (name, tower, flat) before using the app. */
export const profileGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.hasProfile() || inject(Router).createUrlTree(['/signup']);
};

export const platformAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isPlatformAdmin() || inject(Router).createUrlTree(['/']);
};

export const signedOutGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return !auth.signedIn() || inject(Router).createUrlTree(['/']);
};
