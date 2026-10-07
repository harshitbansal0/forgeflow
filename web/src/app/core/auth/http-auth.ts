import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { API_BASE } from '../api/http';
import { UserRole } from '../models';
import { NotifyService } from '../notify.service';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthService).token();
  if (!token || !request.url.startsWith(API_BASE)) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};

/** Shows API problem details in a snackbar and sends expired sessions back to sign-in. */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const notify = inject(NotifyService);
  const router = inject(Router);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && !request.url.endsWith('/auth/login')) {
        if (error.status === 401) {
          auth.logout(router.url);
          notify.error('Your session has expired. Please sign in again.');
        } else {
          notify.error(describeHttpError(error));
        }
      }

      return throwError(() => error);
    })
  );
};

export function describeHttpError(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'Cannot reach the ForgeFlow API. Is the backend running?';
  }

  const problem = error.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | null;
  const validationMessage = problem?.errors ? Object.values(problem.errors).flat()[0] : undefined;
  return validationMessage ?? problem?.detail ?? problem?.title ?? `Request failed (${error.status}).`;
}

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.token() ? true : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export function roleGuard(...roles: UserRole[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    return auth.hasRole(...roles) ? true : router.createUrlTree(['/dashboard']);
  };
}
