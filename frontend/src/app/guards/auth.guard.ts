import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/login';

// Blocks navigation to any route that requires being logged in.
//
// Must return true immediately when not running in the browser: this app
// prerenders most routes at build time (see app.routes.server.ts) and
// server-side-renders the rest, neither of which has a real
// localStorage/request-scoped session to check. That's not a security hole
// — the backend's [Authorize] rejects any unauthenticated API call
// regardless of what this guard did during a build-time/SSR pass; this
// guard is UX (keep a logged-out user from ever seeing a page that has no
// data to show them), not the security boundary.
export const authGuard: CanActivateFn = () => {
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return true;
  }

  if (inject(AuthService).currentUser()) {
    return true;
  }

  return inject(Router).createUrlTree(['/login']);
};
