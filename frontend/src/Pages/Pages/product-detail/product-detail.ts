import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { StatusBadge, StatusBadgeVariant } from '../../../app/shared/status-badge/status-badge';
import { ProductService, Product, LOW_STOCK_THRESHOLD } from '../../../app/services/product';
import { OrderService } from '../../../app/services/orders';
import { AuthService } from '../../../app/services/login';

// One row per (order, this product) pair — a product can only appear once
// per order (see CreateOrderDto), so this is exactly this product's
// movement history: every time it was requested, and whatever eventually
// happened to that request.
interface StockMovement {
  orderId: number;
  orderDate: string;
  type: string;
  quantity: number;
  status: string;
  processedByUsername: string | null;
}

@Component({
  selector: 'app-product-detail',
  imports: [CommonModule, RouterLink, LoadingSpinner, StatusBadge],
  templateUrl: './product-detail.html',
})
export class ProductDetail implements OnInit {
  // Signals, not plain properties — this app has no zone.js, so a plain
  // property mutated inside an RxJS subscribe() callback never triggers a
  // view update. Signals do.
  product = signal<Product | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);
  actionError = signal<string | null>(null);
  lowStockThreshold = LOW_STOCK_THRESHOLD;

  movements = signal<StockMovement[]>([]);
  movementsLoading = signal(true);
  movementColumns = ['orderDate', 'orderId', 'type', 'quantity', 'status', 'processedBy'];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private productService: ProductService,
    private orderService: OrderService,
    public auth: AuthService
  ) {}

  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
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

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.productService.getById(id).subscribe({
      next: (data) => {
        this.product.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.error.set(
          err.status === 404 ? 'Product not found.' : 'Could not load this product. Is the API running?'
        );
        this.loading.set(false);
      },
    });

    // Movement history is every order that ever included this product —
    // reuses the same order list the Orders page already fetches, just
    // filtered down client-side, rather than a dedicated endpoint.
    this.orderService.getAll().subscribe({
      next: (orders) => {
        const rows: StockMovement[] = [];
        for (const order of orders) {
          const item = order.items.find((i) => i.productId === id);
          if (!item) continue;
          rows.push({
            orderId: order.id,
            orderDate: order.orderDate,
            type: order.type,
            quantity: item.quantity,
            status: order.status,
            processedByUsername: order.processedByUsername,
          });
        }
        rows.sort((a, b) => new Date(b.orderDate).getTime() - new Date(a.orderDate).getTime());
        this.movements.set(rows);
        this.movementsLoading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.movementsLoading.set(false);
      },
    });
  }

  deleteProduct(): void {
    const product = this.product();
    if (!product) return;
    if (!confirm(`Delete ${product.name}? You can restore it later from the Products page.`)) return;

    this.actionError.set(null);
    this.productService.delete(product.id).subscribe({
      next: () => this.router.navigate(['/products']),
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not delete this product.');
      },
    });
  }

  restoreProduct(): void {
    const product = this.product();
    if (!product) return;

    this.actionError.set(null);
    this.productService.restore(product.id).subscribe({
      next: (updated) => this.product.set(updated),
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not restore this product.');
      },
    });
  }
}
