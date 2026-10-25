import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Adds the access token to API calls; on 401 renews it once and retries. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || request.url.startsWith('/api/auth/')) {
    return next(request);
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  const withToken = (r: HttpRequest<unknown>) => {
    const token = auth.accessToken;
    return token ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;
  };

  return next(withToken(request)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      return from(auth.refresh()).pipe(
        switchMap((renewed) => {
          if (!renewed) {
            void router.navigate(['/login']);
            return throwError(() => error);
          }
          return next(withToken(request));
        }),
      );
    }),
  );
};
