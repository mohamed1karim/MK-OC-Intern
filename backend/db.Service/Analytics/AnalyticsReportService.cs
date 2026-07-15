// Computes order/demand statistics straight from the database, then hands a
// plain-text summary of them to the LLM (IReportGenerator) to turn into a
// narrative. The stats themselves never depend on the AI call succeeding —
// GenerateReportAsync always returns real numbers, with the narrative field
// falling back to an "unavailable" message if the LLM call fails.
//
// The report is weekly in two senses: the stats only ever cover the last 7
// days (a rolling window, not all-time), and a new one only gets generated
// once a week — WeeklyReportHostedService is what decides *when* to call
// GenerateReportAsync; GetLatestReportAsync is what the API actually serves
// on every page load, reading back whatever was cached last.
using System.Text;
using System.Text.Json;
using db.Context;
using db.Context.Model;
using db.Service.Ai;
using db.Service.DTOs.Analytics;
using Microsoft.EntityFrameworkCore;

namespace db.Service.Analytics;

public class AnalyticsReportService : IAnalyticsReportService
{
    private static readonly TimeSpan ReportWindow = TimeSpan.FromDays(7);

    private readonly AppDbcontext _context;
    private readonly IReportGenerator _reportGenerator;

    public AnalyticsReportService(AppDbcontext context, IReportGenerator reportGenerator)
    {
        _context = context;
        _reportGenerator = reportGenerator;
    }

    // Reads back the most recently generated report, generating one on the
    // spot only if none has ever been stored yet (e.g. right after this
    // feature was deployed, before the background service's first run).
    public async Task<ReportResponseDto> GetLatestReportAsync()
    {
        var latest = await _context.storedReports
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync();

        if (latest is null)
        {
            return await GenerateReportAsync();
        }

        return JsonSerializer.Deserialize<ReportResponseDto>(latest.ReportJson)!;
    }

    public async Task<ReportResponseDto> GenerateReportAsync()
    {
        var windowStart = DateTime.UtcNow - ReportWindow;

        var orders = await _context.orders
            .Include(o => o.OrderItems)
            .Where(o => o.OrderDate >= windowStart)
            .ToListAsync();

        var products = await _context.products.Where(p => !p.IsDeleted).ToListAsync();

        var dto = new ReportResponseDto
        {
            GeneratedAt = DateTime.UtcNow,
            TotalOrders = orders.Count,
            PendingCount = orders.Count(o => o.Status == OrderStatus.Pending),
            ConfirmedCount = orders.Count(o => o.Status == OrderStatus.Confirmed),
            CompletedCount = orders.Count(o => o.Status == OrderStatus.Completed),
            CancelledCount = orders.Count(o => o.Status == OrderStatus.Cancelled),
        };

        var salesValues = orders
            .Where(o => o.Type == OrderType.Out && o.Status == OrderStatus.Completed)
            .Select(o => o.TotalPrice)
            .ToList();
        dto.SalesOrderCount = salesValues.Count;
        dto.MeanSaleValue = Mean(salesValues);
        dto.StdDevSaleValue = StdDev(salesValues);

        var restockValues = orders
            .Where(o => o.Type == OrderType.In && o.Status == OrderStatus.Completed)
            .Select(o => o.TotalPrice)
            .ToList();
        dto.RestockOrderCount = restockValues.Count;
        dto.MeanRestockValue = Mean(restockValues);
        dto.StdDevRestockValue = StdDev(restockValues);

        // "Demand" is defined as completed Out (sale) lines only — Pending/
        // Cancelled orders never actually moved stock, and In orders are
        // restocking, not demand.
        var completedOutItems = orders
            .Where(o => o.Type == OrderType.Out && o.Status == OrderStatus.Completed)
            .SelectMany(o => o.OrderItems)
            .ToList();

        dto.ProductStats = products
            .Select(p =>
            {
                var lines = completedOutItems.Where(i => i.ProductId == p.Id).ToList();
                var quantities = lines.Select(i => (decimal)i.Quantity).ToList();

                return new ProductDemandStatDto
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    CurrentStock = p.Quantity,
                    OrderLineCount = lines.Count,
                    TotalQuantityOrdered = lines.Sum(i => i.Quantity),
                    MeanQuantityPerOrder = Mean(quantities),
                    StdDevQuantityPerOrder = StdDev(quantities),
                    Revenue = lines.Sum(i => i.Quantity * i.UnitPrice),
                };
            })
            .OrderByDescending(s => s.TotalQuantityOrdered)
            .ToList();

        dto.Narrative = await _reportGenerator.GenerateAsync(BuildStatsSummary(dto));

        _context.storedReports.Add(new StoredReport
        {
            GeneratedAt = dto.GeneratedAt,
            ReportJson = JsonSerializer.Serialize(dto),
        });
        await _context.SaveChangesAsync();

        return dto;
    }

    private static string BuildStatsSummary(ReportResponseDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"Order status breakdown (last 7 days, {dto.TotalOrders} total): " +
            $"Pending={dto.PendingCount}, Confirmed={dto.ConfirmedCount}, " +
            $"Completed={dto.CompletedCount}, Cancelled={dto.CancelledCount}");
        sb.AppendLine(
            $"Completed sales (Out) orders: n={dto.SalesOrderCount}, " +
            $"mean value=${dto.MeanSaleValue}, std dev=${dto.StdDevSaleValue}");
        sb.AppendLine(
            $"Completed restock (In) orders: n={dto.RestockOrderCount}, " +
            $"mean value=${dto.MeanRestockValue}, std dev=${dto.StdDevRestockValue}");
        sb.AppendLine();
        sb.AppendLine("Per-product demand over the last 7 days (from completed sales orders only, best sellers first):");
        foreach (var p in dto.ProductStats)
        {
            sb.AppendLine(
                $"- {p.ProductName}: current stock={p.CurrentStock}, sold in {p.OrderLineCount} orders, " +
                $"total units sold={p.TotalQuantityOrdered}, mean units/order={p.MeanQuantityPerOrder}, " +
                $"std dev units/order={p.StdDevQuantityPerOrder}, revenue=${p.Revenue}");
        }
        return sb.ToString();
    }

    private static decimal Mean(IReadOnlyCollection<decimal> values) =>
        values.Count == 0 ? 0m : Math.Round(values.Average(), 2);

    // Sample standard deviation (n-1 denominator) — these are historical
    // orders treated as a sample of the product's ongoing demand pattern,
    // not the entire population of all orders that will ever exist.
    private static decimal StdDev(IReadOnlyCollection<decimal> values)
    {
        if (values.Count < 2) return 0m;

        var mean = values.Average();
        var sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Round((decimal)Math.Sqrt((double)(sumOfSquares / (values.Count - 1))), 2);
    }
}
