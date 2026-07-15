import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { StatusBadge, StatusBadgeVariant } from '../../../app/shared/status-badge/status-badge';
import { OrderService, Order } from '../../../app/services/orders';
import { AuthService } from '../../../app/services/login';
import { downloadExcel } from '../../../app/utils/excel';
import { EgpCurrencyPipe } from '../../../app/pipes/egp-currency.pipe';

type TypeFilter = 'All' | 'In' | 'Out';
type StatusFilter = 'All' | 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled';

// This page is just a summary list now — click any row to see the full
// breakdown (items, who requested/accepted it, progress) on the Order
// Detail page, which is also where the Confirm/Complete/Cancel actions live.
@Component({
  selector: 'app-orders',
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinner, StatusBadge, EgpCurrencyPipe],
  templateUrl: './orders.html',
})
export class Orders implements OnInit {
  orders = signal<Order[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  typeFilter = signal<TypeFilter>('All');
  statusFilter = signal<StatusFilter>('All');
  minTotal = signal<number | null>(null);
  maxTotal = signal<number | null>(null);

  filteredOrders = computed(() => {
    const type = this.typeFilter();
    const status = this.statusFilter();
    const min = this.minTotal();
    const max = this.maxTotal();

    return this.orders().filter((o) => {
      if (type !== 'All' && o.type !== type) return false;
      if (status !== 'All' && o.status !== status) return false;
      if (min !== null && o.totalPrice < min) return false;
      if (max !== null && o.totalPrice > max) return false;
      return true;
    });
  });

  constructor(private orderService: OrderService, public auth: AuthService) {}

  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  ngOnInit(): void {
    const user = this.auth.currentUser();
    if (!user) {
      this.loading.set(false);
      return;
    }

    const request = this.isAdmin
      ? this.orderService.getAll()
      : this.orderService.getAll(user.id);

    request.subscribe({
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
  }

  clearFilters(): void {
    this.typeFilter.set('All');
    this.statusFilter.set('All');
    this.minTotal.set(null);
    this.maxTotal.set(null);
  }

  statusVariant(status: string): StatusBadgeVariant {
    switch (status) {
      case 'Confirmed':
        return 'amber';
      case 'Completed':
        return 'green';
      case 'Cancelled':
        return 'gray';
      default:
        return 'indigo';
    }
  }

  exportExcel(): void {
    const headers = ['Order ID', 'Type', 'Status', 'Order Date', 'Total', 'Requested By', 'Processed By'];
    const rows = this.filteredOrders().map((o) => [
      o.id,
      o.type,
      o.status,
      o.orderDate,
      o.totalPrice,
      o.createdByUsername,
      o.processedByUsername ?? '',
    ]);
    downloadExcel('orders.xlsx', headers, rows);
  }
}
