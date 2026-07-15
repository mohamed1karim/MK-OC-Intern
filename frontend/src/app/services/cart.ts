import { Injectable, signal, computed, effect } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { OrderService, Order } from './orders';
import { Product } from './product';

export interface CartItem {
  productId: number;
  productName: string;
  price: number;
  quantity: number;
}

const CART_STORAGE_KEY = 'cart';

// A cart holds items for exactly one order Type at a time — the backend
// models Type at the whole-order level (not per line), so a single cart
// can't mix "stock coming in" with "stock going out". Switching the type
// while the cart has items clears it, rather than silently reinterpreting
// what's already in there.
@Injectable({ providedIn: 'root' })
export class CartService {
  type = signal<'In' | 'Out'>('Out');
  items = signal<CartItem[]>([]);

  totalItems = computed(() => this.items().reduce((sum, i) => sum + i.quantity, 0));
  totalPrice = computed(() => this.items().reduce((sum, i) => sum + i.quantity * i.price, 0));

  constructor(private orderService: OrderService) {
    const stored = this.readStored();
    if (stored) {
      this.type.set(stored.type);
      this.items.set(stored.items);
    }

    // Persisted to localStorage (not just kept in memory) — otherwise a
    // full page reload, or opening the app in a new tab, would silently
    // lose whatever was in the cart. Runs automatically whenever type or
    // items changes, same idea as AuthService persisting the logged-in user.
    effect(() => {
      this.persist(this.type(), this.items());
    });
  }

  setType(type: 'In' | 'Out'): void {
    if (type === this.type()) return;
    if (this.items().length > 0) {
      this.items.set([]);
    }
    this.type.set(type);
  }

  addItem(product: Product, quantity: number): void {
    if (quantity <= 0) return;

    this.items.update((list) => {
      const existing = list.find((i) => i.productId === product.id);
      if (existing) {
        return list.map((i) =>
          i.productId === product.id ? { ...i, quantity: i.quantity + quantity } : i
        );
      }
      return [...list, { productId: product.id, productName: product.name, price: product.price, quantity }];
    });
  }

  updateQuantity(productId: number, quantity: number): void {
    if (quantity <= 0) {
      this.removeItem(productId);
      return;
    }
    this.items.update((list) => list.map((i) => (i.productId === productId ? { ...i, quantity } : i)));
  }

  removeItem(productId: number): void {
    this.items.update((list) => list.filter((i) => i.productId !== productId));
  }

  clear(): void {
    this.items.set([]);
  }

  checkout(): Observable<Order> {
    return this.orderService
      .create({
        type: this.type(),
        items: this.items().map((i) => ({ productId: i.productId, quantity: i.quantity })),
      })
      .pipe(tap(() => this.clear()));
  }

  private persist(type: 'In' | 'Out', items: CartItem[]): void {
    if (typeof localStorage === 'undefined') return;
    localStorage.setItem(CART_STORAGE_KEY, JSON.stringify({ type, items }));
  }

  private readStored(): { type: 'In' | 'Out'; items: CartItem[] } | null {
    if (typeof localStorage === 'undefined') return null;
    const raw = localStorage.getItem(CART_STORAGE_KEY);
    return raw ? JSON.parse(raw) : null;
  }
}
