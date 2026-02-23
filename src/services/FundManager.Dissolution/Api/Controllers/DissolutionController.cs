using FundManager.BuildingBlocks.Domain;
using FundManager.Dissolution.Domain.Entities;
using FundManager.Dissolution.Domain.Services;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundManager.Dissolution.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/dissolution")]
[Authorize(Policy = FundAuthorizationPolicies.FundMember)]
public class DissolutionController : ControllerBase
{
    private readonly DissolutionService _service;

    public DissolutionController(DissolutionService service)
    {
        _service = service;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value ?? throw new UnauthorizedAccessException());

    /// <summary>POST /api/funds/{fundId}/dissolution/initiate</summary>
    [HttpPost("initiate")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> Initiate(Guid fundId, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _service.InitiateDissolutionAsync(fundId, userId, ct);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                "CONFLICT" => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };

        return Ok(ToSettlementDto(result.Value!));
    }

    /// <summary>GET /api/funds/{fundId}/dissolution/settlement</summary>
    [HttpGet("settlement")]
    public async Task<IActionResult> GetSettlement(Guid fundId, CancellationToken ct)
    {
        var result = await _service.GetSettlementAsync(fundId, ct);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };

        var settlement = result.Value!;
        var canConfirm = settlement.LineItems.All(li => li.NetPayout >= 0);
        var blockers = settlement.LineItems
            .Where(li => li.NetPayout < 0)
            .Select(li => new DissolutionBlockerDto
            {
                UserId = li.UserId,
                NetPayout = li.NetPayout,
                OutstandingAmount = li.OutstandingLoanPrincipal + li.UnpaidInterest + li.UnpaidDues,
            })
            .ToList();

        return Ok(new SettlementDetailDto
        {
            Settlement = ToSettlementDto(settlement),
            LineItems = settlement.LineItems.Select(ToLineItemDto).ToList(),
            CanConfirm = canConfirm,
            Blockers = blockers,
        });
    }

    /// <summary>POST /api/funds/{fundId}/dissolution/settlement/recalculate</summary>
    [HttpPost("settlement/recalculate")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> Recalculate(Guid fundId, CancellationToken ct)
    {
        var result = await _service.CalculateSettlementAsync(fundId, ct);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new { message = result.Error }),
                "CONFLICT" => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };

        var settlement = result.Value!;
        var canConfirm = settlement.LineItems.All(li => li.NetPayout >= 0);
        var blockers = settlement.LineItems
            .Where(li => li.NetPayout < 0)
            .Select(li => new DissolutionBlockerDto
            {
                UserId = li.UserId,
                NetPayout = li.NetPayout,
                OutstandingAmount = li.OutstandingLoanPrincipal + li.UnpaidInterest + li.UnpaidDues,
            })
            .ToList();

        return Ok(new SettlementDetailDto
        {
            Settlement = ToSettlementDto(settlement),
            LineItems = settlement.LineItems.Select(ToLineItemDto).ToList(),
            CanConfirm = canConfirm,
            Blockers = blockers,
        });
    }

    /// <summary>POST /api/funds/{fundId}/dissolution/confirm</summary>
    [HttpPost("confirm")]
    [Authorize(Policy = FundAuthorizationPolicies.FundAdminOrAbove)]
    public async Task<IActionResult> Confirm(Guid fundId, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _service.ConfirmDissolutionAsync(fundId, userId, ct);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new { message = result.Error }),
                "CONFLICT" => Conflict(new { message = result.Error }),
                "VALIDATION" => UnprocessableEntity(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };

        return Ok(ToSettlementDto(result.Value!));
    }

    /// <summary>GET /api/funds/{fundId}/dissolution/report?format=pdf|csv</summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        Guid fundId,
        [FromQuery] string format,
        [FromServices] SettlementReportGenerator reportGen,
        CancellationToken ct)
    {
        var result = await _service.GetSettlementAsync(fundId, ct);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };

        var settlement = result.Value!;

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = reportGen.GenerateCsv(settlement);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"settlement-{fundId}.csv");
        }

        var pdf = reportGen.GeneratePdf(settlement);
        return File(pdf, "application/pdf", $"settlement-{fundId}.pdf");
    }

    // ── Mapping ─────────────────────────────────

    private static DissolutionSettlementDto ToSettlementDto(DissolutionSettlement s) => new()
    {
        Id = s.Id,
        FundId = s.FundId,
        TotalInterestPool = s.TotalInterestPool,
        TotalContributionsCollected = s.TotalContributionsCollected,
        Status = s.Status.ToString(),
        SettlementDate = s.SettlementDate?.ToString("yyyy-MM-dd"),
        ConfirmedBy = s.ConfirmedBy,
        CreatedAt = s.CreatedAt,
    };

    private static DissolutionLineItemDto ToLineItemDto(DissolutionLineItem li) => new()
    {
        UserId = li.UserId,
        TotalPaidContributions = li.TotalPaidContributions,
        InterestShare = li.InterestShare,
        OutstandingLoanPrincipal = li.OutstandingLoanPrincipal,
        UnpaidInterest = li.UnpaidInterest,
        UnpaidDues = li.UnpaidDues,
        GrossPayout = li.GrossPayout,
        NetPayout = li.NetPayout,
    };
}

// ── DTOs ──────────────────────────────────────

public class DissolutionSettlementDto
{
    public Guid Id { get; init; }
    public Guid FundId { get; init; }
    public decimal TotalInterestPool { get; init; }
    public decimal TotalContributionsCollected { get; init; }
    public string Status { get; init; } = default!;
    public string? SettlementDate { get; init; }
    public Guid? ConfirmedBy { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class SettlementDetailDto
{
    public DissolutionSettlementDto Settlement { get; init; } = default!;
    public List<DissolutionLineItemDto> LineItems { get; init; } = new();
    public bool CanConfirm { get; init; }
    public List<DissolutionBlockerDto> Blockers { get; init; } = new();
}

public class DissolutionLineItemDto
{
    public Guid UserId { get; init; }
    public decimal TotalPaidContributions { get; init; }
    public decimal InterestShare { get; init; }
    public decimal OutstandingLoanPrincipal { get; init; }
    public decimal UnpaidInterest { get; init; }
    public decimal UnpaidDues { get; init; }
    public decimal GrossPayout { get; init; }
    public decimal NetPayout { get; init; }
}

public class DissolutionBlockerDto
{
    public Guid UserId { get; init; }
    public string? UserName { get; init; }
    public decimal NetPayout { get; init; }
    public decimal OutstandingAmount { get; init; }
}
