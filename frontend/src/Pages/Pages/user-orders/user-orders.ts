import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { StatusBadge, StatusBadgeVariant } from '../../../app/shared/status-badge/status-badge';
import { OrderService, Order } from '../../../app/services/orders';
import { UsersService } from '../../../app/services/users';
import { AuthService, User } from '../../../app/services/login';
import { EgpCurrencyPipe } from '../../../app/pipes/egp-currency.pipe';

// Lets an Admin/SuperAdmin drill into one specific person's order history
// from the Users page. Uses the backend's involvingUserId filter, so this
// shows every order where the target user was either the creator or the
// processor — not just orders they placed themselves. That matters for an
// Admin target: their history includes orders they accepted/completed/
// cancelled on top of anything they created.
@Component({
  selector: 'app-user-orders',
  imports: [CommonModule, RouterLink, LoadingSpinner, StatusBadge, EgpCurrencyPipe],
  templateUrl: './user-orders.html',
})
export class UserOrders implements OnInit {
  targetUser = signal<User | null>(null);
  orders = signal<Order[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  targetUserId = 0;

  constructor(
    private route: ActivatedRoute,
    private orderService: OrderService,
    private usersService: UsersService,
    public auth: AuthService
  ) {}

  // A plain Admin may drill into any User's history, but only a SuperAdmin
  // may drill into another Admin's history (an Admin viewing a peer Admin's
  // history isn't part of the spec, so it stays SuperAdmin-only).
  get canView(): boolean {
    const viewerRole = this.auth.currentUser()?.role;
    const targetRole = this.targetUser()?.role;
    if (!viewerRole || !targetRole) return false;
    if (viewerRole === 'SuperAdmin') return targetRole === 'User' || targetRole === 'Admin';
    if (viewerRole === 'Admin') return targetRole === 'User';
    return false;
  }

  ngOnInit(): void {
    this.targetUserId = Number(this.route.snapshot.paramMap.get('id'));

    if (!this.auth.currentUser()) {
      this.loading.set(false);
      return;
    }

    this.usersService.getById(this.targetUserId).subscribe({
      next: (user) => {
        this.targetUser.set(user);
        if (!this.canView) {
          this.loading.set(false);
          return;
        }
        this.orderService.getAll(undefined, this.targetUserId).subscribe({
          next: (data) => {
            this.orders.set(data);
            this.loading.set(false);
          },
          error: (err) => {
            console.error(err);
            this.error.set('Could not load orders. Is the API running?');
            this.loading.set(false);
          },
        });
      },
      error: (err) => {
        console.error(err);
        this.error.set(err.status === 404 ? 'User not found.' : 'Could not load this user. Is the API running?');
        this.loading.set(false);
      },
    });
  }

  wasCreatedBy(order: Order): boolean {
    return order.createdByUserId === this.targetUserId;
  }

  wasProcessedBy(order: Order): boolean {
    return order.processedByUserId === this.targetUserId;
  }

  // The order's status tells us which action the processor actually took
  // (Confirm, Complete, or Cancel) — "Processed" on its own doesn't say
  // which, so name the tag after the real action instead. There's no
  // separate status badge on this page (it duplicated whichever of these
  // words already applied), so this is the only place that status shows up.
  processedActionLabel(order: Order): string {
    switch (order.status) {
      case 'Completed':
        return 'Completed';
      case 'Cancelled':
        return 'Cancelled';
      default:
        return 'Confirmed';
    }
  }

  processedTagVariant(order: Order): StatusBadgeVariant {
    switch (this.processedActionLabel(order)) {
      case 'Confirmed':
        return 'amber';
      case 'Completed':
        return 'green';
      case 'Cancelled':
        return 'gray';
      default:
        return 'gray';
    }
  }
}
