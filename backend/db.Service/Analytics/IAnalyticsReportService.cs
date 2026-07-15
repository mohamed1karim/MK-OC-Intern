using db.Service.DTOs.Analytics;

namespace db.Service.Analytics;

public interface IAnalyticsReportService
{
    // Recomputes stats for the last 7 days, calls the LLM, and caches the
    // result — this is the "generate a new report" action, called by
    // WeeklyReportHostedService on its weekly cadence and by the
    // Admin-facing "regenerate now" button.
    Task<ReportResponseDto> GenerateReportAsync();

    // Reads back the most recently cached report — what GET
    // /api/analytics/report actually serves on every page load.
    Task<ReportResponseDto> GetLatestReportAsync();
}
