import { Injectable, Inject, PLATFORM_ID, signal, computed, effect } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { forkJoin, interval } from 'rxjs';
import { ProductService, LOW_STOCK_THRESHOLD } from './product';
import { OrderService } from './orders';
import { AuthService } from './login';

// How often to re-poll while the app is open — no push infrastructure here,
// so this is what stands in for "live" updates. Short enough that a new
// pending order or a stock drop shows up without the user needing to click
// the bell themselves.
const POLL_INTERVAL_MS = 30_000;

export interface AppNotification {
  // Stable across refreshes for the same underlying event, so "seen" state
  // (persisted in localStorage) survives a page reload instead of every
  // notification looking "new" again after every refresh.
  id: string;
  message: string;
  icon: string;
  link: string[];
  createdAt: string;
}

const SEEN_STORAGE_KEY = 'wms_seen_notification_ids';

// A lightweight, no-backend notification system: derives "events" from data
// this app already fetches elsewhere (products/orders) rather than standing
// up push infrastructure. Admins see low-stock products and orders awaiting
// confirmation; regular Users see their own orders' status changes. Read
// state is just a set of seen ids in localStorage. Refreshes on a fixed
// poll interval, immediately on login/logout, and again whenever the bell
// is opened.
@Injectable({ providedIn: 'root' })
export class NotificationService {
  notifications = signal<AppNotification[]>([]);
  unreadCount = computed(() => this.notifications().filter((n) => !this.seenIds.has(n.id)).length);

  private seenIds = new Set<string>();
  private isBrowser: boolean;

  constructor(
    private productService: ProductService,
    private orderService: OrderService,
    private auth: AuthService,
    @Inject(PLATFORM_ID) platformId: Object
  ) {
    this.isBrowser = isPlatformBrowser(platformId);
    if (this.isBrowser) {
      try {
        const raw = localStorage.getItem(SEEN_STORAGE_KEY);
        if (raw) this.seenIds = new Set(JSON.parse(raw));
      } catch {
        // Corrupt/foreign localStorage value — treat as "nothing seen yet".
      }

      // Refetch immediately whenever who's logged in changes (login/logout
      // without a full page reload), not just on the fixed poll cadence —
      // effect() runs once right away and again on every currentUser() change.
      effect(() => {
        this.auth.currentUser();
        this.refresh();
      });

      // refresh() itself no-ops (clears the list, no HTTP call) while logged
      // out, so this can just run continuously without an unsubscribe.
      interval(POLL_INTERVAL_MS).subscribe(() => this.refresh());
    }
  }

  private get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  refresh(): void {
    const user = this.auth.currentUser();
    if (!user) {
      this.notifications.set([]);
      return;
    }

    if (this.isAdmin) {
      forkJoin({
        products: this.productService.getAll(),
        orders: this.orderService.getAll(),
      }).subscribe(({ products, orders }) => {
        const list: AppNotification[] = [];

        for (const p of products.filter((p) => p.quantity <= LOW_STOCK_THRESHOLD)) {
          list.push({
            id: `low-stock-${p.id}`,
            message: `${p.name} is low on stock (${p.quantity} left)`,
            icon: 'warning',
            link: ['/products', p.id.toString()],
            createdAt: p.updatedAt,
          });
        }

        for (const o of orders.filter((o) => o.status === 'Pending')) {
          list.push({
            id: `pending-order-${o.id}`,
            message: `Order #${o.id} by ${o.createdByUsername} needs confirmation`,
            icon: 'pending_actions',
            link: ['/orders', o.id.toString()],
            createdAt: o.orderDate,
          });
        }

        this.setSorted(list);
      });
    } else {
      this.orderService.getAll(user.id).subscribe((orders) => {
        const list: AppNotification[] = orders
          .filter((o) => o.status === 'Completed' || o.status === 'Cancelled' || o.status === 'Confirmed')
          .map((o) => ({
            id: `order-status-${o.id}-${o.status}`,
            message: `Your order #${o.id} was ${o.status.toLowerCase()}`,
            icon: o.status === 'Cancelled' ? 'cancel' : 'check_circle',
            link: ['/orders', o.id.toString()],
            createdAt: o.orderDate,
          }));

        this.setSorted(list);
      });
    }
  }

  markAllSeen(): void {
    for (const n of this.notifications()) {
      this.seenIds.add(n.id);
    }
    if (this.isBrowser) {
      localStorage.setItem(SEEN_STORAGE_KEY, JSON.stringify([...this.seenIds]));
    }
  }

  private setSorted(list: AppNotification[]): void {
    list.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    this.notifications.set(list);
  }
}
