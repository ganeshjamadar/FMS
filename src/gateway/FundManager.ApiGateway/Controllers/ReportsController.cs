using FundManager.ApiGateway.Services;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundManager.ApiGateway.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/reports")]
[Authorize(Policy = FundAuthorizationPolicies.FundMember)]
public class ReportsController : ControllerBase
{
    private readonly ReportAggregationService _aggregation;

    public ReportsController(ReportAggregationService aggregation)
    {
        _aggregation = aggregation;
    }

    private string? GetAuthHeader() =>
        Request.Headers.TryGetValue("Authorization", out var h) ? h.ToString() : null;

    /// <summary>
    /// GET /api/funds/{fundId}/reports/contribution-summary?fromMonth=202401&amp;toMonth=202412&amp;format=json|pdf|csv
    /// </summary>
    [HttpGet("contribution-summary")]
    public async Task<IActionResult> GetContributionReport(
        Guid fundId,
        [FromQuery] int fromMonth,
        [FromQuery] int toMonth,
        [FromQuery] string format = "json",
        [FromServices] Reports.CsvReportGenerator? csvGen = null,
        [FromServices] Reports.PdfReportGenerator? pdfGen = null,
        CancellationToken ct = default)
    {
        var report = await _aggregation.GetContributionReportAsync(fundId, fromMonth, toMonth, GetAuthHeader(), ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) && csvGen is not null)
        {
            var csv = csvGen.GenerateContributionReport(report);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"contribution-summary-{fundId}.csv");
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) && pdfGen is not null)
        {
            var pdf = pdfGen.GenerateContributionReport(report);
            return File(pdf, "application/pdf", $"contribution-summary-{fundId}.pdf");
        }

        return Ok(report);
    }

    /// <summary>
    /// GET /api/funds/{fundId}/reports/loan-portfolio?format=json|pdf|csv
    /// </summary>
    [HttpGet("loan-portfolio")]
    public async Task<IActionResult> GetLoanPortfolioReport(
        Guid fundId,
        [FromQuery] string format = "json",
        [FromServices] Reports.CsvReportGenerator? csvGen = null,
        [FromServices] Reports.PdfReportGenerator? pdfGen = null,
        CancellationToken ct = default)
    {
        var report = await _aggregation.GetLoanPortfolioReportAsync(fundId, GetAuthHeader(), ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) && csvGen is not null)
        {
            var csv = csvGen.GenerateLoanPortfolioReport(report);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"loan-portfolio-{fundId}.csv");
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) && pdfGen is not null)
        {
            var pdf = pdfGen.GenerateLoanPortfolioReport(report);
            return File(pdf, "application/pdf", $"loan-portfolio-{fundId}.pdf");
        }

        return Ok(report);
    }

    /// <summary>
    /// GET /api/funds/{fundId}/reports/interest-earnings?fromMonth=202401&amp;toMonth=202412&amp;format=json|pdf|csv
    /// </summary>
    [HttpGet("interest-earnings")]
    public async Task<IActionResult> GetInterestEarningsReport(
        Guid fundId,
        [FromQuery] int fromMonth,
        [FromQuery] int toMonth,
        [FromQuery] string format = "json",
        [FromServices] Reports.CsvReportGenerator? csvGen = null,
        [FromServices] Reports.PdfReportGenerator? pdfGen = null,
        CancellationToken ct = default)
    {
        var report = await _aggregation.GetInterestEarningsReportAsync(fundId, fromMonth, toMonth, GetAuthHeader(), ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) && csvGen is not null)
        {
            var csv = csvGen.GenerateInterestEarningsReport(report);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"interest-earnings-{fundId}.csv");
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) && pdfGen is not null)
        {
            var pdf = pdfGen.GenerateInterestEarningsReport(report);
            return File(pdf, "application/pdf", $"interest-earnings-{fundId}.pdf");
        }

        return Ok(report);
    }

    /// <summary>
    /// GET /api/funds/{fundId}/reports/balance-sheet?fromMonth=202401&amp;toMonth=202412&amp;format=json|pdf|csv
    /// </summary>
    [HttpGet("balance-sheet")]
    public async Task<IActionResult> GetBalanceSheet(
        Guid fundId,
        [FromQuery] int fromMonth,
        [FromQuery] int toMonth,
        [FromQuery] string format = "json",
        [FromServices] Reports.CsvReportGenerator? csvGen = null,
        [FromServices] Reports.PdfReportGenerator? pdfGen = null,
        CancellationToken ct = default)
    {
        var report = await _aggregation.GetBalanceSheetAsync(fundId, fromMonth, toMonth, GetAuthHeader(), ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) && csvGen is not null)
        {
            var csv = csvGen.GenerateBalanceSheet(report);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"balance-sheet-{fundId}.csv");
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) && pdfGen is not null)
        {
            var pdf = pdfGen.GenerateBalanceSheet(report);
            return File(pdf, "application/pdf", $"balance-sheet-{fundId}.pdf");
        }

        return Ok(report);
    }

    /// <summary>
    /// GET /api/funds/{fundId}/reports/member/{userId}/statement?format=json|pdf|csv
    /// </summary>
    [HttpGet("member/{userId:guid}/statement")]
    public async Task<IActionResult> GetMemberStatement(
        Guid fundId,
        Guid userId,
        [FromQuery] string format = "json",
        [FromServices] Reports.CsvReportGenerator? csvGen = null,
        [FromServices] Reports.PdfReportGenerator? pdfGen = null,
        CancellationToken ct = default)
    {
        var report = await _aggregation.GetMemberStatementAsync(fundId, userId, GetAuthHeader(), ct);

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase) && csvGen is not null)
        {
            var csv = csvGen.GenerateMemberStatement(report);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"member-statement-{userId}.csv");
        }

        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase) && pdfGen is not null)
        {
            var pdf = pdfGen.GenerateMemberStatement(report);
            return File(pdf, "application/pdf", $"member-statement-{userId}.pdf");
        }

        return Ok(report);
    }
}
