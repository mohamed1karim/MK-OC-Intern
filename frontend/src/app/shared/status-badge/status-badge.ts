import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type StatusBadgeVariant = 'indigo' | 'amber' | 'violet' | 'green' | 'red' | 'gray';

// Replaces <mat-chip highlighted>. Deliberately generic — it doesn't know
// about "role" or "order status" or "low stock", it just renders a colored
// pill for whichever variant the caller picks. The domain-to-color mapping
// (e.g. "Cancelled" -> gray) stays in each consuming page, same as today's
// [class.type-out]/[class.role-admin] bindings.
@Component({
  selector: 'app-status-badge',
  imports: [CommonModule],
  template: `
    <span
      class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
      [ngClass]="classesFor(variant())"
    >
      {{ label() }}
    </span>
  `,
})
export class StatusBadge {
  label = input.required<string | number>();
  variant = input.required<StatusBadgeVariant>();

  classesFor(variant: StatusBadgeVariant): string {
    const map: Record<StatusBadgeVariant, string> = {
      indigo: 'bg-badge-indigo-bg text-badge-indigo-text',
      amber: 'bg-badge-amber-bg text-badge-amber-text',
      violet: 'bg-badge-violet-bg text-badge-violet-text',
      green: 'bg-badge-green-bg text-badge-green-text',
      red: 'bg-badge-red-bg text-badge-red-text',
      gray: 'bg-badge-gray-bg text-badge-gray-text',
    };
    return map[variant];
  }
}
