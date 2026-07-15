import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { StatusBadge } from '../../../app/shared/status-badge/status-badge';
import { OrderService, Order } from '../../../app/services/orders';
import { AuthService } from '../../../app/services/login';
import { EgpCurrencyPipe } from '../../../app/pipes/egp-currency.pipe';

@Component({
  selector: 'app-order-detail',
  imports: [CommonModule, RouterLink, LoadingSpinner, StatusBadge, EgpCurrencyPipe],
  templateUrl: './order-detail.html',
})
export class OrderDetail implements OnInit {
  // Signals, not plain properties — this app has no zone.js, so a plain
  // property mutated inside an RxJS subscribe() callback never triggers a
  // view update. Signals do.
  order = signal<Order | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  actionError = signal<string | null>(null);

  // The 3 forward steps of a non-cancelled order's progress. Cancelled is a
  // separate terminal branch, not a 4th forward step — shown as its own
  // banner instead of a stepper position.
  readonly steps = ['Pending', 'Confirmed', 'Completed'];
  readonly itemColumns = ['product', 'quantity', 'unitPrice', 'lineTotal'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private orderService: OrderService,
    public auth: AuthService
  ) {}

  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  get currentStepIndex(): number {
    const status = this.order()?.status;
    return this.steps.indexOf(status ?? '');
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.orderService.getById(id).subscribe({
      next: (data) => {
        this.order.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.error.set(
          err.status === 404 ? 'Order not found.' : 'Could not load this order. Is the API running?'
        );
        this.loading.set(false);
      },
    });
  }

  confirm(): void {
    this.runAction((id) => this.orderService.confirm(id));
  }

  complete(): void {
    this.runAction((id) => this.orderService.complete(id));
  }

  cancel(): void {
    this.runAction((id) => this.orderService.cancel(id));
  }

  deleteOrder(): void {
    const order = this.order();
    if (!order) return;

    this.actionError.set(null);
    this.orderService.delete(order.id).subscribe({
      next: () => this.router.navigate(['/orders']),
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not delete this order.');
      },
    });
  }

  private runAction(call: (id: number) => ReturnType<OrderService['confirm']>): void {
    const order = this.order();
    if (!order) return;

    this.actionError.set(null);
    call(order.id).subscribe({
      next: (updated) => this.order.set(updated),
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not update this order.');
      },
    });
  }
}
