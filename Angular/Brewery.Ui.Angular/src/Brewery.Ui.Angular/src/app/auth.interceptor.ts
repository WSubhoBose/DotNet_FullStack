import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(AuthService).token;
  if (!token || request.url.endsWith('/api/auth/token')) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { Auth: token } }));
};
