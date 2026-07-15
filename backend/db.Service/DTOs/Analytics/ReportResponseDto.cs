// Response shape for GET /api/analytics/report — the raw computed stats
// (so the frontend can render real numbers, not just prose) plus the LLM's
// narrative built from those same numbers.
namespace db.Service.DTOs.Analytics;

public class ReportResponseDto
{
    public DateTime GeneratedAt { get; set; }

    public int TotalOrders { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }

    // Stats over completed "Out" (sales) orders only.
    public int SalesOrderCount { get; set; }
    public decimal MeanSaleValue { get; set; }
    public decimal StdDevSaleValue { get; set; }

    // Stats over completed "In" (restock) orders only.
    public int RestockOrderCount { get; set; }
    public decimal MeanRestockValue { get; set; }
    public decimal StdDevRestockValue { get; set; }

    // Sorted by TotalQuantityOrdered descending — best sellers first.
    public List<ProductDemandStatDto> ProductStats { get; set; } = new();

    // The LLM-written overview + buy/don't-buy recommendations.
    public string Narrative { get; set; } = string.Empty;
}

public class ProductDemandStatDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CurrentStock { get; set; }

    // All figures below are computed from completed "Out" (sales) orders only.
    public int OrderLineCount { get; set; }
    public int TotalQuantityOrdered { get; set; }
    public decimal MeanQuantityPerOrder { get; set; }
    public decimal StdDevQuantityPerOrder { get; set; }
    public decimal Revenue { get; set; }
}
