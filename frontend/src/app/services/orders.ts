import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// Matches the backend's OrderItemResponseDto.
export interface OrderItem {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

// Matches the backend's OrderResponseDto.
export interface Order {
  id: number;
  type: string;
  status: string;
  orderDate: string;
  totalPrice: number;
  createdByUserId: number;
  createdByUsername: string;
  processedByUserId: number | null;
  // Null while still Pending — nobody has acted on it yet.
  processedByUsername: string | null;
  items: OrderItem[];
}

// Matches the backend's CreateOrderDto/OrderItemInputDto. Anyone (User or
// Admin) can create an order and picks the type themselves — the spec
// never ties order Type to the creator's role. No createdByUserId here —
// the backend always attributes the order to the caller, from their JWT.
export interface CreateOrderRequest {
  type: string;
  items: { productId: number; quantity: number }[];
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  private apiUrl = environment.apiUrl + '/orders';

  constructor(private http: HttpClient) {}

  // Pass createdByUserId to get only orders that user created (used by the
  // "my orders" page); pass involvingUserId to get orders where that user
  // was either the creator OR the processor (used by the Admin/SuperAdmin
  // "view this person's order history" page — an Admin's history includes
  // orders they accepted/completed/cancelled, not just ones they created);
  // omit both to get every order (used for Admins viewing all orders).
  getAll(createdByUserId?: number, involvingUserId?: number): Observable<Order[]> {
    const params: string[] = [];
    if (createdByUserId) params.push(`createdByUserId=${createdByUserId}`);
    if (involvingUserId) params.push(`involvingUserId=${involvingUserId}`);
    const url = params.length ? `${this.apiUrl}?${params.join('&')}` : this.apiUrl;
    return this.http.get<Order[]>(url);
  }

  getById(id: number): Observable<Order> {
    return this.http.get<Order>(`${this.apiUrl}/${id}`);
  }

  create(dto: CreateOrderRequest): Observable<Order> {
    return this.http.post<Order>(this.apiUrl, dto);
  }

  // Admin-only actions, enforced server-side via [Authorize(Roles = ...)]
  // using the caller's identity from their JWT. Order: confirm -> complete/cancel.
  confirm(id: number): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/confirm`, {});
  }

  complete(id: number): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/complete`, {});
  }

  cancel(id: number): Observable<Order> {
    return this.http.post<Order>(`${this.apiUrl}/${id}/cancel`, {});
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
