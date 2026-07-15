// The "front desk" for analytics: /api/analytics. Admin/SuperAdmin only —
// same gating as the frontend Analytics page already enforces client-side.
using db.Service.Analytics;
using db.Service.DTOs.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace db.api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsReportService _reportService;

    public AnalyticsController(IAnalyticsReportService reportService)
    {
        _reportService = reportService;
    }

    // GET /api/analytics/report — returns the latest cached weekly report
    // (WeeklyReportHostedService is what actually (re)generates it). Fast:
    // no LLM call on a normal page load, just a DB read.
    [HttpGet("report")]
    public async Task<ActionResult<ReportResponseDto>> GetReport()
    {
        var report = await _reportService.GetLatestReportAsync();
        return Ok(report);
    }

    // POST /api/analytics/report/regenerate — forces a fresh report right
    // now instead of waiting for the weekly cadence. Can take a few seconds
    // (live AI call).
    [HttpPost("report/regenerate")]
    public async Task<ActionResult<ReportResponseDto>> Regenerate()
    {
        var report = await _reportService.GenerateReportAsync();
        return Ok(report);
    }
}
