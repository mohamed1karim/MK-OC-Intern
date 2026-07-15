import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

// Matches the backend's UserResponseDto (no password field, ever).
export interface User {
  id: number;
  username: string;
  email: string;
  role: string;
  createdAt: string;
  isDeleted: boolean;
}

const STORAGE_KEY = 'currentUser';
const TOKEN_STORAGE_KEY = 'authToken';

// Matches the backend's LoginResponseDto.
interface LoginResponse {
  user: User;
  token: string;
  expiresAt: string;
}

// Real JWT auth: login/signup hit the API, the returned token is attached
// to every subsequent request by auth.interceptor.ts, and route guards
// (auth.guard.ts/admin.guard.ts) gate navigation using currentUser().
@Injectable({ providedIn: 'root' })
export class AuthService {
  private apiUrl = environment.apiUrl + '/auth';
  private usersUrl = environment.apiUrl + '/users';

  // Signal so components can reactively read the current user. Initialized
  // from localStorage so a page refresh doesn't log the user out.
  currentUser = signal<User | null>(this.readStoredUser());

  constructor(private http: HttpClient) {}

  login(username: string, password: string): Observable<User> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, { username, password }).pipe(
      tap((response) => this.setSession(response)),
      map((response) => response.user)
    );
  }

  // Read by auth.interceptor.ts to attach "Authorization: Bearer <token>"
  // to outgoing requests.
  getToken(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  // Just account creation — reuses the existing POST /api/users endpoint
  // (Users CRUD), hardcoding role "User" since this is self-service signup,
  // not an admin creating an account. The backend hashes the password;
  // this doesn't log the new user in (login.ts calls login() right after,
  // with the same credentials, for a smoother signup -> logged-in flow).
  signup(username: string, email: string, password: string): Observable<User> {
    return this.http.post<User>(this.usersUrl, { username, email, password, role: 'User' });
  }

  logout(): void {
    this.setSession(null);
  }

  private setSession(response: LoginResponse | null): void {
    this.currentUser.set(response?.user ?? null);
    if (typeof localStorage === 'undefined') return;

    if (response) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(response.user));
      localStorage.setItem(TOKEN_STORAGE_KEY, response.token);
    } else {
      localStorage.removeItem(STORAGE_KEY);
      localStorage.removeItem(TOKEN_STORAGE_KEY);
    }
  }

  private readStoredUser(): User | null {
    if (typeof localStorage === 'undefined') return null;
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as User) : null;
  }
}
