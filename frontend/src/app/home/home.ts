import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { StatusBadge, StatusBadgeVariant } from '../shared/status-badge/status-badge';
import { AuthService } from '../services/login';
import { ProductService, Product, LOW_STOCK_THRESHOLD } from '../services/product';
import { OrderService, Order } from '../services/orders';

@Component({
  selector: 'app-home',
  imports: [CommonModule, RouterLink, StatusBadge],
  templateUrl: './home.html',
})
export class Home implements OnInit {
  loadingStats = signal(true);

  totalProducts = signal(0);
  lowStockCount = signal(0);
  pendingOrdersCount = signal(0);
  recentOrders = signal<Order[]>([]);
  lowStockProducts = signal<Product[]>([]);

  constructor(
    public auth: AuthService,
    private productService: ProductService,
    private orderService: OrderService
  ) {}

  // SuperAdmin has every Admin power plus more (see UserRole's backend doc
  // comment), so the Admin-only "Users" button is available to both.
  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  get greeting(): string {
    const hour = new Date().getHours();
    if (hour < 12) return 'Good morning';
    if (hour < 18) return 'Good afternoon';
    return 'Good evening';
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
    const user = this.auth.currentUser();
    if (!user) {
      this.loadingStats.set(false);
      return;
    }

    // Admins see how many orders are pending system-wide (something needs
    // their attention); regular Users see how many of their own are pending.
    const ordersRequest = this.isAdmin ? this.orderService.getAll() : this.orderService.getAll(user.id);

    forkJoin({
      products: this.productService.getAll(),
      orders: ordersRequest,
    }).subscribe({
      next: ({ products, orders }) => {
        this.totalProducts.set(products.length);
        const lowStock = products
          .filter((p) => p.quantity <= LOW_STOCK_THRESHOLD)
          .sort((a, b) => a.quantity - b.quantity);
        this.lowStockCount.set(lowStock.length);
        this.lowStockProducts.set(lowStock.slice(0, 5));
        this.pendingOrdersCount.set(orders.filter((o) => o.status === 'Pending').length);

        const sorted = [...orders].sort(
          (a, b) => new Date(b.orderDate).getTime() - new Date(a.orderDate).getTime()
        );
        this.recentOrders.set(sorted.slice(0, 5));

        this.loadingStats.set(false);
      },
      error: () => {
        // Stats are a nice-to-have on this dashboard, not critical — fail
        // quietly and just leave the cards at their zeroed defaults rather
        // than blocking the whole page behind an error message.
        this.loadingStats.set(false);
      },
    });
  }
}
