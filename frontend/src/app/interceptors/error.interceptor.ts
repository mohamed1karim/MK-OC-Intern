import { HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/login';

// Runs for every HTTP request the app makes (registered once in
// app.config.ts, not per-service). Centralizes error logging in one place
// instead of every component repeating its own console.error() — a
// consistent [status] METHOD url line makes API failures easy to spot in
// devtools regardless of which page triggered them.
//
// The error is re-thrown unchanged: components still set their own
// user-facing message (e.g. "Could not load products" vs "Could not load
// users") since only they know the right contextual fallback text — this
// interceptor only adds a diagnostic side-channel, it doesn't replace that.
//
// A 401 additionally forces a logout + redirect to /login — it means the
// token is missing, invalid, or expired, so whatever page the user was on
// can't be trusted to keep working. There's no refresh-token flow; this is
// the entire expiry-handling story.
//
// This must only run in the browser. During SSR/prerendering there is no
// localStorage, so every authenticated page's data-fetching calls get a
// 401 from the server for a perfectly valid, still-logged-in user — acting
// on that server-side would make the server render (and serve!) the login
// page for a direct navigation/hard refresh to any authenticated route,
// even though the browser holds a perfectly good token. The real, valid
// 401 (bad/expired token) only ever happens once hydrated in the browser.
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  return next(req).pipe(
    catchError((error) => {
      const status = error.status === 0 ? 'no connection' : error.status;
      console.error(`[HTTP ${status}] ${req.method} ${req.url}`, error.error ?? error.message);

      if (error.status === 401 && isBrowser) {
        auth.logout();
        router.navigate(['/login']);
      }

      return throwError(() => error);
    })
  );
};
