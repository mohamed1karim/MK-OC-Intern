import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { StatusBadge, StatusBadgeVariant } from '../../../app/shared/status-badge/status-badge';
import { OrderService } from '../../../app/services/orders';
import { AuthService } from '../../../app/services/login';
import { downloadExcel } from '../../../app/utils/excel';

type TypeFilter = 'All' | 'In' | 'Out';
type StatusFilter = 'All' | 'Pending' | 'Confirmed' | 'Completed' | 'Cancelled';

// One row per (order, product) line across every order in the system —
// the same flattening product-detail.ts already does for a single
// product's history, just without filtering down to one productId.
interface StockMovement {
  orderId: number;
  orderDate: string;
  productId: number;
  productName: string;
  type: string;
  quantity: number;
  status: string;
  createdByUsername: string;
  processedByUsername: string | null;
}

@Component({
  selector: 'app-stock-history',
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinner, StatusBadge],
  templateUrl: './stock-history.html',
})
export class StockHistory implements OnInit {
  movements = signal<StockMovement[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  // 0 means "All products" — a real product id is never 0, so this avoids
  // needing a separate string/number union just for the "no filter" case.
  productFilter = signal(0);
  typeFilter = signal<TypeFilter>('All');
  statusFilter = signal<StatusFilter>('All');

  // Populated from whatever products actually appear in the loaded
  // movements — a plain select instead of free-text search, so filtering
  // is an exact id match instead of a substring match that can surprise
  // (e.g. "Coca" also matching a future "Coca-something" product).
  productOptions = computed(() => {
    const seen = new Map<number, string>();
    for (const m of this.movements()) {
      if (!seen.has(m.productId)) seen.set(m.productId, m.productName);
    }
    return [...seen.entries()]
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  filteredMovements = computed(() => {
    const productId = this.productFilter();
    const type = this.typeFilter();
    const status = this.statusFilter();

    return this.movements().filter((m) => {
      if (productId !== 0 && m.productId !== productId) return false;
      if (type !== 'All' && m.type !== type) return false;
      if (status !== 'All' && m.status !== status) return false;
      return true;
    });
  });

  constructor(private orderService: OrderService, public auth: AuthService) {}

  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  ngOnInit(): void {
    if (!this.isAdmin) {
      this.loading.set(false);
      return;
    }

    this.orderService.getAll().subscribe({
      next: (orders) => {
        const rows: StockMovement[] = [];
        for (const order of orders) {
          for (const item of order.items) {
            rows.push({
              orderId: order.id,
              orderDate: order.orderDate,
              productId: item.productId,
              productName: item.productName,
              type: order.type,
              quantity: item.quantity,
              status: order.status,
              createdByUsername: order.createdByUsername,
              processedByUsername: order.processedByUsername,
            });
          }
        }
        rows.sort((a, b) => new Date(b.orderDate).getTime() - new Date(a.orderDate).getTime());
        this.movements.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.error.set('Could not load stock movement history. Is the API running?');
        this.loading.set(false);
      },
    });
  }

  clearFilters(): void {
    this.productFilter.set(0);
    this.typeFilter.set('All');
    this.statusFilter.set('All');
  }

  // "+2"/"-3" — the sign alone communicates In/Out at a glance in the
  // Quantity column, alongside the existing Type badge.
  signedQuantity(m: StockMovement): string {
    return (m.type === 'In' ? '+' : '-') + m.quantity;
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
    const headers = ['Date', 'Order ID', 'Product', 'Type', 'Quantity', 'Status', 'Requested By', 'Processed By'];
    const rows = this.filteredMovements().map((m) => [
      m.orderDate,
      m.orderId,
      m.productName,
      m.type,
      m.quantity,
      m.status,
      m.createdByUsername,
      m.processedByUsername ?? '',
    ]);
    downloadExcel('stock-movement-history.xlsx', headers, rows);
  }
}
