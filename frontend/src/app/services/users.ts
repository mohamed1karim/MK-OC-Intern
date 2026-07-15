import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { User } from './login';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private apiUrl = environment.apiUrl + '/users';

  constructor(private http: HttpClient) {}

  // includeDeleted shows soft-deleted users too (Admin/SuperAdmin "show
  // deleted" toggle) — omit it (or pass false) for the normal active-only view.
  getAll(includeDeleted = false): Observable<User[]> {
    const url = includeDeleted ? `${this.apiUrl}?includeDeleted=true` : this.apiUrl;
    return this.http.get<User[]>(url);
  }

  getById(id: number): Observable<User> {
    return this.http.get<User>(`${this.apiUrl}/${id}`);
  }

  // Admin/SuperAdmin only — enforced server-side via [Authorize(Roles = ...)],
  // using the caller's identity from their JWT. Backend also rejects
  // promoting/demoting a SuperAdmin.
  promoteToAdmin(id: number): Observable<User> {
    return this.http.post<User>(`${this.apiUrl}/${id}/promote`, {});
  }

  demoteToUser(id: number): Observable<User> {
    return this.http.post<User>(`${this.apiUrl}/${id}/demote`, {});
  }

  // Admin/SuperAdmin only — and the backend rejects deleting anyone who
  // isn't currently a plain User (an Admin/SuperAdmin target must be
  // demoted first). Soft delete — the user row stays (so their order
  // history keeps resolving), just flagged IsDeleted and hidden from the
  // normal list.
  deleteUser(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  restoreUser(id: number): Observable<User> {
    return this.http.post<User>(`${this.apiUrl}/${id}/restore`, {});
  }
}
