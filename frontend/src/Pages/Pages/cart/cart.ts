import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { CartService } from '../../../app/services/cart';
import { AuthService } from '../../../app/services/login';

@Component({
  selector: 'app-cart',
  imports: [CommonModule, RouterLink],
  templateUrl: './cart.html',
})
export class Cart {
  error = signal<string | null>(null);
  loading = signal(false);

  constructor(public cart: CartService, public auth: AuthService, private router: Router) {}

  increment(productId: number, currentQty: number): void {
    this.cart.updateQuantity(productId, currentQty + 1);
  }

  decrement(productId: number, currentQty: number): void {
    this.cart.updateQuantity(productId, currentQty - 1);
  }

  remove(productId: number): void {
    this.cart.removeItem(productId);
  }

  checkout(): void {
    this.error.set(null);
    this.loading.set(true);

    this.cart.checkout().subscribe({
      next: (order) => {
        this.loading.set(false);
        this.router.navigate(['/orders', order.id]);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.error ?? 'Could not place order.');
      },
    });
  }
}
