import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/login';

// Attaches "Authorization: Bearer <token>" to every outgoing request when a
// token is stored — this is the "token interceptor" the auth bonus asks
// for. Registered before errorInterceptor in app.config.ts so the token is
// already on the request by the time any error handling runs.
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(AuthService).getToken();

  if (!token) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
