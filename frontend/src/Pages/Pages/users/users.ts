import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { StatusBadge, StatusBadgeVariant } from '../../../app/shared/status-badge/status-badge';
import { UsersService } from '../../../app/services/users';
import { AuthService, User } from '../../../app/services/login';

@Component({
  selector: 'app-users',
  imports: [CommonModule, FormsModule, RouterLink, LoadingSpinner, StatusBadge],
  templateUrl: './users.html',
})
export class Users implements OnInit {
  // Signals, not plain properties — this app has no zone.js, so a plain
  // property mutated inside an RxJS subscribe() callback never triggers a
  // view update. Signals do.
  users = signal<User[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  actionError = signal<string | null>(null);
  showDeleted = signal(false);

  constructor(private usersService: UsersService, public auth: AuthService) {}

  // SuperAdmin has every Admin power plus more (see UserRole's backend doc
  // comment), so this page — and the promote/demote actions on it — are
  // available to both.
  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  ngOnInit(): void {
    // No route guard yet (per the simple-login approach) — if a non-Admin
    // somehow lands here, just skip loading; the template shows a message.
    if (!this.isAdmin) {
      this.loading.set(false);
      return;
    }

    this.loadUsers();
  }

  toggleShowDeleted(): void {
    this.showDeleted.update((v) => !v);
    this.loadUsers();
  }

  private loadUsers(): void {
    this.loading.set(true);
    this.usersService.getAll(this.showDeleted()).subscribe({
      next: (data) => {
        this.users.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.error.set('Could not load users. Is the API running?');
        this.loading.set(false);
      },
    });
  }

  makeAdmin(user: User): void {
    this.actionError.set(null);
    this.usersService.promoteToAdmin(user.id).subscribe({
      next: (updated) => {
        this.users.update((list) => list.map((u) => (u.id === updated.id ? updated : u)));
      },
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not promote user.');
      },
    });
  }

  demote(user: User): void {
    this.actionError.set(null);
    this.usersService.demoteToUser(user.id).subscribe({
      next: (updated) => {
        this.users.update((list) => list.map((u) => (u.id === updated.id ? updated : u)));
      },
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not demote user.');
      },
    });
  }

  roleVariant(role: string): StatusBadgeVariant {
    switch (role) {
      case 'Admin':
        return 'amber';
      case 'SuperAdmin':
        return 'violet';
      default:
        return 'indigo';
    }
  }

  // A plain Admin may drill into any User's order history, but only a
  // SuperAdmin may drill into another Admin's (SuperAdmin has every Admin
  // power plus more, so both can view a User's; only SuperAdmin outranks an
  // Admin enough to view theirs).
  canViewOrders(target: User): boolean {
    const viewerRole = this.auth.currentUser()?.role;
    if (viewerRole === 'SuperAdmin') return target.role === 'User' || target.role === 'Admin';
    if (viewerRole === 'Admin') return target.role === 'User';
    return false;
  }

  deleteUser(user: User): void {
    if (!confirm(`Delete ${user.username}? You can restore them later from "Show deleted".`)) return;

    this.actionError.set(null);
    this.usersService.deleteUser(user.id).subscribe({
      next: () => this.loadUsers(),
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not delete user.');
      },
    });
  }

  restoreUser(user: User): void {
    this.actionError.set(null);
    this.usersService.restoreUser(user.id).subscribe({
      next: () => this.loadUsers(),
      error: (err) => {
        this.actionError.set(err.error?.error ?? 'Could not restore user.');
      },
    });
  }
}
