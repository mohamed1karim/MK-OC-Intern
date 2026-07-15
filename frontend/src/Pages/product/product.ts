import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../app/shared/loading-spinner/loading-spinner';
import { StatusBadge } from '../../app/shared/status-badge/status-badge';
import { ProductService, LOW_STOCK_THRESHOLD } from '../../app/services/product';
import { Product } from '../../app/services/product';
import { CartService } from '../../app/services/cart';
import { AuthService } from '../../app/services/login';
import { downloadCsv } from '../../app/utils/csv';

@Component({
 selector: 'app-products',
 templateUrl: './product.html',
 imports: [CommonModule, FormsModule, RouterLink, LoadingSpinner, StatusBadge],
})
export class ProductsComponent implements OnInit {
 // Signals, not plain properties — this app has no zone.js, so a plain
 // property mutated inside an RxJS subscribe() callback never triggers a
 // view update. Signals do.
 products = signal<Product[]>([]);
 loading = signal(true);
 error = signal<string | null>(null);

 // Per-product quantity draft, keyed by product id. A plain object (not a
 // signal) is fine here — only ever read when "Add to Cart" is clicked, and
 // the [(ngModel)] input already reflects what the user typed via the
 // browser's own DOM — no re-render needed for that.
 quantityDrafts: Record<number, number> = {};

 // Filter state. Products load once and filtering happens client-side
 // (computed signal) — the list is small enough that a round trip to the
 // server for every keystroke would just be slower, not more correct.
 searchText = signal('');
 minPrice = signal<number | null>(null);
 maxPrice = signal<number | null>(null);
 lowStockOnly = signal(false);
 showDeleted = signal(false);

 lowStockThreshold = LOW_STOCK_THRESHOLD;

 filteredProducts = computed(() => {
  const search = this.searchText().trim().toLowerCase();
  const min = this.minPrice();
  const max = this.maxPrice();
  const lowStockOnly = this.lowStockOnly();

  return this.products().filter((p) => {
   if (search && !p.name.toLowerCase().includes(search) && !p.description.toLowerCase().includes(search)) {
    return false;
   }
   if (min !== null && p.price < min) return false;
   if (max !== null && p.price > max) return false;
   if (lowStockOnly && !this.isLowStock(p)) return false;
   return true;
  });
 });

 lowStockProducts = computed(() => this.products().filter((p) => this.isLowStock(p)));

 constructor(
  private productService: ProductService,
  public cart: CartService,
  public auth: AuthService
 ) {}

 get isAdmin(): boolean {
  const role = this.auth.currentUser()?.role;
  return role === 'Admin' || role === 'SuperAdmin';
 }

 ngOnInit(): void {
  this.loadProducts();
 }

 toggleShowDeleted(): void {
  this.showDeleted.update((v) => !v);
  this.loadProducts();
 }

 private loadProducts(): void {
  this.loading.set(true);
  this.productService.getAll(this.showDeleted()).subscribe({
   next: (data) => {
    this.products.set(data);
    for (const p of data) {
     this.quantityDrafts[p.id] ??= 1;
    }
    this.loading.set(false);
   },
   error: (err) => {
    console.error(err);
    this.error.set('Could not load products. Is the API running?');
    this.loading.set(false);
   }
  });
 }

 restoreProduct(product: Product): void {
  this.error.set(null);
  this.productService.restore(product.id).subscribe({
   next: () => this.loadProducts(),
   error: (err) => {
    this.error.set(err.error?.error ?? 'Could not restore product.');
   },
  });
 }

 isLowStock(p: Product): boolean {
  return p.quantity <= this.lowStockThreshold;
 }

 clearFilters(): void {
  this.searchText.set('');
  this.minPrice.set(null);
  this.maxPrice.set(null);
  this.lowStockOnly.set(false);
 }

 addToCart(product: Product): void {
  const quantity = this.quantityDrafts[product.id] ?? 1;
  this.cart.addItem(product, quantity);
 }

 // Exports exactly what's currently on screen (respecting the active
 // search/price/low-stock filters), not the full unfiltered list — what you
 // exported matches what you were looking at.
 exportCsv(): void {
  const headers = ['Name', 'Description', 'Price', 'Quantity', 'Added By', 'Created At', 'Updated At', 'Deleted'];
  const rows = this.filteredProducts().map((p) => [
   p.name,
   p.description,
   p.price,
   p.quantity,
   p.createdByUsername ?? '',
   p.createdAt,
   p.updatedAt,
   p.isDeleted ? 'Yes' : 'No',
  ]);
  downloadCsv('products.csv', headers, rows);
 }
}
