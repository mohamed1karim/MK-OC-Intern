import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/login';

// Blocks navigation to an Admin/SuperAdmin-only route (Users, Analytics).
// Applied alongside authGuard, not instead of it — this only adds the role
// check on top. Same SSR/prerender caveat as authGuard: return true
// immediately outside the browser, since the backend's
// [Authorize(Roles = ...)] is the real boundary either way.
export const adminGuard: CanActivateFn = () => {
  if (!isPlatformBrowser(inject(PLATFORM_ID))) {
    return true;
  }

  const role = inject(AuthService).currentUser()?.role;
  if (role === 'Admin' || role === 'SuperAdmin') {
    return true;
  }

  return inject(Router).createUrlTree(['/home']);
};
