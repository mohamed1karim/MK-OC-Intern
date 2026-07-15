import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

// Matches the backend's ProductDemandStatDto (db.Service/DTOs/Analytics).
export interface ProductDemandStat {
  productId: number;
  productName: string;
  currentStock: number;
  orderLineCount: number;
  totalQuantityOrdered: number;
  meanQuantityPerOrder: number;
  stdDevQuantityPerOrder: number;
  revenue: number;
}

// Matches the backend's ReportResponseDto.
export interface AnalyticsReport {
  generatedAt: string;
  totalOrders: number;
  pendingCount: number;
  confirmedCount: number;
  completedCount: number;
  cancelledCount: number;
  salesOrderCount: number;
  meanSaleValue: number;
  stdDevSaleValue: number;
  restockOrderCount: number;
  meanRestockValue: number;
  stdDevRestockValue: number;
  productStats: ProductDemandStat[];
  narrative: string;
}

@Injectable({ providedIn: 'root' })
export class AnalyticsReportService {
  private apiUrl = environment.apiUrl + '/analytics';
  constructor(private http: HttpClient) {}

  // Admin/SuperAdmin only — enforced server-side. Reads back whatever the
  // backend's weekly background job generated last — fast, no LLM call —
  // so this is safe to call automatically on every page load.
  getReport(): Observable<AnalyticsReport> {
    return this.http.get<AnalyticsReport>(`${this.apiUrl}/report`);
  }

  // Forces a fresh report right now instead of waiting for the weekly
  // cadence — a live LLM call, so only fire this from an explicit
  // "Regenerate now" click, not automatically.
  regenerate(): Observable<AnalyticsReport> {
    return this.http.post<AnalyticsReport>(`${this.apiUrl}/report/regenerate`, {});
  }
}
