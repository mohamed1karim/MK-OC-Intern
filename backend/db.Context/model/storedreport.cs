// A cached copy of the last AI-generated analytics report — GET
// /api/analytics/report reads the latest row here instead of recomputing
// stats and calling the LLM on every page load; a background service
// (WeeklyReportHostedService) is what actually creates new rows, once a
// week.
namespace db.Context.Model;

public class StoredReport
{
    public int Id { get; set; }

    public DateTime GeneratedAt { get; set; }

    // The full ReportResponseDto, serialized — simplest way to cache a
    // shape that already has its own DTO/JSON contract with the frontend
    // without mapping every stat field onto its own column.
    public string ReportJson { get; set; } = string.Empty;
}
