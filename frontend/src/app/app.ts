import { Component, signal, Inject, PLATFORM_ID } from '@angular/core';
import { CommonModule, isPlatformBrowser, CurrencyPipe } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './services/login';
import { CartService } from './services/cart';
import { NotificationService } from './services/notification';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, CurrencyPipe],
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('frontend');

  // Tracks the current URL so the cart FAB can hide itself while already on
  // the /cart page — showing it there is pure redundant clutter since
  // clicking it just re-navigates to the page you're standing on.
  currentUrl = signal('');

  // Drives the sidebar's layout: a permanent column on wide screens, an
  // off-canvas drawer (toggled via sidenavOpen) on narrow ones.
  isHandset = signal(false);
  sidenavOpen = signal(false);
  accountMenuOpen = signal(false);
  notificationMenuOpen = signal(false);

  constructor(
    public auth: AuthService,
    public cart: CartService,
    public notifications: NotificationService,
    private router: Router,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {
    this.currentUrl.set(this.router.url);
    this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe((e) => this.currentUrl.set((e as NavigationEnd).urlAfterRedirects));

    // NotificationService drives its own initial fetch + poll interval +
    // login/logout re-fetch internally (see notification.ts) — nothing to
    // kick off here beyond injecting it above.

    // window.matchMedia has no meaning during SSR/prerendering — guard the
    // same way auth.guard.ts/admin.guard.ts already do, rather than
    // introducing a new pattern for the same problem.
    if (isPlatformBrowser(this.platformId)) {
      const mql = window.matchMedia('(max-width: 768px)');
      this.isHandset.set(mql.matches);
      this.sidenavOpen.set(!mql.matches);
      mql.addEventListener('change', (e) => {
        this.isHandset.set(e.matches);
        this.sidenavOpen.set(!e.matches);
      });
    }
  }

  // SuperAdmin has every Admin power plus more (see UserRole's backend doc
  // comment), so the Admin-only "Users" nav link is available to both.
  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  toggleSidenav(): void {
    this.sidenavOpen.update((v) => !v);
  }

  closeSidenavOnMobile(): void {
    if (this.isHandset()) {
      this.sidenavOpen.set(false);
    }
  }

  toggleAccountMenu(): void {
    this.accountMenuOpen.update((v) => !v);
  }

  toggleNotificationMenu(): void {
    const opening = !this.notificationMenuOpen();
    this.notificationMenuOpen.set(opening);
    if (opening) {
      this.notifications.refresh();
      this.notifications.markAllSeen();
    }
  }

  logout(): void {
    this.accountMenuOpen.set(false);
    this.auth.logout();
    this.router.navigate(['/login']);
  }

  goToCart(): void {
    this.router.navigate(['/cart']);
  }
}
