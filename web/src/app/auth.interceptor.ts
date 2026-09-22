import { HttpErrorResponse } from '@angular/common/http';
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(request.clone({ withCredentials: true })).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !request.url.endsWith('/auth/me')) {
        auth.user.set(null);
        void router.navigateByUrl('/login');
      }
      return throwError(() => error);
    }),
  );
};