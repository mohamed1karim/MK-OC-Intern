import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// Matches the backend's ProductResponseDto (db.Service/DTOs/Products).
export interface Product {
  id: number;
  name: string;
  // Short description — shown in list views (the Products table, etc.).
  description: string;
  // AI-expanded version of description — shown on the product's own
  // detail page instead.
  longDescription: string;
  price: number;
  quantity: number;
  createdAt: string;
  updatedAt: string;
  createdByUserId: number | null;
  // Null for products created before this field existed.
  createdByUsername: string | null;
  isDeleted: boolean;
}

// Matches the backend's CreateProductDto. No actingUserId/role here — the
// backend derives who's acting from the JWT (attached by auth.interceptor.ts),
// and enforces Admin/SuperAdmin via [Authorize(Roles = ...)].
export interface CreateProductRequest {
  name: string;
  description: string;
  price: number;
  quantity: number;
}

// Matches the backend's UpdateProductDto. No quantity field — stock can
// only ever change through Orders, never by editing a product directly.
export interface UpdateProductRequest {
  name: string;
  description: string;
  price: number;
}

// Shared with the Products list, Home dashboard, and the low-stock banner
// so "low stock" means the same thing everywhere instead of a magic number
// copy-pasted in three places.
export const LOW_STOCK_THRESHOLD = 5;

@Injectable({ providedIn: 'root' })
export class ProductService {
  private apiUrl = environment.apiUrl + '/products';
  constructor(private http: HttpClient) {}

  // includeDeleted shows soft-deleted products too (Admin/SuperAdmin "show
  // deleted" toggle) — omit it (or pass false) for the normal active-only view.
  getAll(includeDeleted = false): Observable<Product[]> {
    const url = includeDeleted ? `${this.apiUrl}?includeDeleted=true` : this.apiUrl;
    return this.http.get<Product[]>(url);
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`${this.apiUrl}/${id}`);
  }

  create(dto: CreateProductRequest): Observable<Product> {
    return this.http.post<Product>(this.apiUrl, dto);
  }

  // Admin/SuperAdmin only — enforced server-side via [Authorize(Roles = ...)].
  update(id: number, dto: UpdateProductRequest): Observable<Product> {
    return this.http.put<Product>(`${this.apiUrl}/${id}`, dto);
  }

  // Admin/SuperAdmin only — enforced server-side.
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  restore(id: number): Observable<Product> {
    return this.http.post<Product>(`${this.apiUrl}/${id}/restore`, {});
  }
}
