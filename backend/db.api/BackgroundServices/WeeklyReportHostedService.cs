// Runs for the lifetime of the app: checks whether a week has passed since
// the last stored analytics report, and if so, generates a fresh one. This
// is what makes the AI report "weekly" rather than something an Admin has
// to remember to (re)trigger — GET /api/analytics/report just reads back
// whatever this produced most recently.
using db.Context;
using db.Service.Analytics;
using Microsoft.EntityFrameworkCore;

namespace db.api.BackgroundServices;

public class WeeklyReportHostedService : BackgroundService
{
    private static readonly TimeSpan ReportInterval = TimeSpan.FromDays(7);

    // How often to check whether it's due — frequent enough that the report
    // never drifts far past its weekly due date, cheap enough (one small
    // DB read most of the time) that running it this often doesn't matter.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;

    public WeeklyReportHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateIfDueAsync(stoppingToken);
            }
            catch
            {
                // A transient DB/LLM hiccup here shouldn't take down the
                // whole app's background host — just try again next tick.
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // App is shutting down — exit the loop instead of throwing.
            }
        }
    }

    private async Task GenerateIfDueAsync(CancellationToken stoppingToken)
    {
        // Scoped services (AppDbcontext, IAnalyticsReportService) can't be
        // injected straight into a singleton BackgroundService — create a
        // fresh scope for each check instead, same pattern any ASP.NET Core
        // background job needs.
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbcontext>();

        var latestGeneratedAt = await context.storedReports
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => (DateTime?)r.GeneratedAt)
            .FirstOrDefaultAsync(stoppingToken);

        if (latestGeneratedAt is not null && latestGeneratedAt.Value > DateTime.UtcNow - ReportInterval)
        {
            return;
        }

        var reportService = scope.ServiceProvider.GetRequiredService<IAnalyticsReportService>();
        await reportService.GenerateReportAsync();
    }
}
