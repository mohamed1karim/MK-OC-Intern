import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { forkJoin } from 'rxjs';
import { LoadingSpinner } from '../../../app/shared/loading-spinner/loading-spinner';
import { ProductService, Product, LOW_STOCK_THRESHOLD } from '../../../app/services/product';
import { OrderService, Order } from '../../../app/services/orders';
import { AuthService } from '../../../app/services/login';
import { AnalyticsReportService, AnalyticsReport } from '../../../app/services/analytics-report';

// The LLM narrative comes back as "## Heading" sections with "- " bullet
// lines underneath — parsed here into a small structure so the template can
// render real headings/lists instead of dumping raw markdown-ish text.
interface ReportSection {
  heading: string;
  bullets: string[];
  paragraphs: string[];
}

function parseNarrative(text: string): ReportSection[] {
  const sections: ReportSection[] = [];
  let current: ReportSection | null = null;

  for (const rawLine of text.split('\n')) {
    const line = rawLine.trim();
    if (!line) continue;

    if (line.startsWith('## ')) {
      current = { heading: line.slice(3).trim(), bullets: [], paragraphs: [] };
      sections.push(current);
    } else if (current) {
      if (line.startsWith('- ')) {
        current.bullets.push(line.slice(2).trim());
      } else {
        current.paragraphs.push(line);
      }
    }
  }

  return sections;
}

interface StockBar {
  productId: number;
  name: string;
  quantity: number;
  isLow: boolean;
  widthPercent: number;
}

// Fixed display order — a workflow-stage axis, never re-sorted by count, so
// the same status always sits in the same slot/color across every render
// (the "categorical hue in fixed order" rule from the dataviz skill).
const STATUS_ORDER = ['Pending', 'Confirmed', 'Completed', 'Cancelled'] as const;

interface StatusBar {
  status: string;
  count: number;
  heightPercent: number;
  colorClass: string;
}

@Component({
  selector: 'app-analytics',
  imports: [CommonModule, LoadingSpinner],
  templateUrl: './analytics.html',
  styleUrl: './analytics.css',
})
export class Analytics implements OnInit {
  loading = signal(true);
  error = signal<string | null>(null);

  stockBars = signal<StockBar[]>([]);
  statusBars = signal<StatusBar[]>([]);

  hoveredStock = signal<StockBar | null>(null);
  hoveredStatus = signal<StatusBar | null>(null);

  totalOrders = computed(() => this.statusBars().reduce((sum, b) => sum + b.count, 0));

  // The AI report is now weekly: the backend regenerates it on its own
  // cadence (WeeklyReportHostedService), so loading it here is just a cheap
  // read of whatever's cached — safe to fire alongside the charts above.
  // Only "regenerate now" (an explicit live LLM call) is a deliberate,
  // separate action.
  reportLoading = signal(true);
  regenerating = signal(false);
  reportError = signal<string | null>(null);
  report = signal<AnalyticsReport | null>(null);
  reportSections = computed(() => {
    const r = this.report();
    return r ? parseNarrative(r.narrative) : [];
  });

  constructor(
    private productService: ProductService,
    private orderService: OrderService,
    private analyticsReportService: AnalyticsReportService,
    public auth: AuthService
  ) {}

  get isAdmin(): boolean {
    const role = this.auth.currentUser()?.role;
    return role === 'Admin' || role === 'SuperAdmin';
  }

  ngOnInit(): void {
    if (!this.isAdmin) {
      this.loading.set(false);
      this.reportLoading.set(false);
      return;
    }

    forkJoin({
      products: this.productService.getAll(),
      orders: this.orderService.getAll(),
    }).subscribe({
      next: ({ products, orders }) => {
        this.stockBars.set(this.buildStockBars(products));
        this.statusBars.set(this.buildStatusBars(orders));
        this.loading.set(false);
      },
      error: (err) => {
        console.error(err);
        this.error.set('Could not load analytics. Is the API running?');
        this.loading.set(false);
      },
    });

    this.analyticsReportService.getReport().subscribe({
      next: (report) => {
        this.report.set(report);
        this.reportLoading.set(false);
      },
      error: (err) => {
        this.reportError.set(err.error?.error ?? 'Could not load the weekly AI report.');
        this.reportLoading.set(false);
      },
    });
  }

  regenerateReport(): void {
    this.reportError.set(null);
    this.regenerating.set(true);

    this.analyticsReportService.regenerate().subscribe({
      next: (report) => {
        this.report.set(report);
        this.regenerating.set(false);
      },
      error: (err) => {
        this.reportError.set(err.error?.error ?? 'Could not regenerate the AI report.');
        this.regenerating.set(false);
      },
    });
  }

  private buildStockBars(products: Product[]): StockBar[] {
    // Lowest stock first — the products most likely to need attention lead
    // the chart instead of being buried at the bottom.
    const sorted = [...products].sort((a, b) => a.quantity - b.quantity);
    const max = Math.max(...sorted.map((p) => p.quantity), 1);

    return sorted.map((p) => ({
      productId: p.id,
      name: p.name,
      quantity: p.quantity,
      isLow: p.quantity <= LOW_STOCK_THRESHOLD,
      widthPercent: (p.quantity / max) * 100,
    }));
  }

  private buildStatusBars(orders: Order[]): StatusBar[] {
    const counts: Record<string, number> = {};
    for (const status of STATUS_ORDER) counts[status] = 0;
    for (const order of orders) {
      if (order.status in counts) counts[order.status]++;
    }

    const max = Math.max(...Object.values(counts), 1);

    return STATUS_ORDER.map((status, i) => ({
      status,
      count: counts[status],
      heightPercent: (counts[status] / max) * 100,
      colorClass: `status-slot-${i + 1}`,
    }));
  }
}
