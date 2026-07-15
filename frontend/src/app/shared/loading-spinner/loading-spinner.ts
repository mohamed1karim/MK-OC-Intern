import { Component, input } from '@angular/core';

// Replaces <mat-spinner>. Just Tailwind's built-in animate-spin utility on a
// bordered circle — no custom keyframes needed. Only owns the spinner
// graphic; the accompanying "Loading…" text stays in each page's own
// wrapper, same as it did with mat-spinner.
@Component({
  selector: 'app-loading-spinner',
  template: `
    <div
      class="animate-spin rounded-full border-4 border-gray-200 border-t-blue-600 dark:border-gray-700 dark:border-t-blue-400"
      [style.width.px]="size()"
      [style.height.px]="size()"
    ></div>
  `,
})
export class LoadingSpinner {
  size = input(28);
}
