using FundManager.Contributions.Domain.Services;
using FundManager.BuildingBlocks.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundManager.Contributions.Api.Controllers;

[ApiController]
[Route("api/funds/{fundId:guid}/contributions/payments")]
[Authorize(Policy = FundAuthorizationPolicies.FundEditorOrAbove)]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentsController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// POST /api/funds/{fundId}/contributions/payments — Record a contribution payment.
    /// Requires Idempotency-Key header (FR-114) and If-Match header (FR-035a).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RecordPayment(
        Guid fundId,
        [FromBody] RecordPaymentRequestDto request,
        CancellationToken ct)
    {
        // Extract required headers
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKeyHeader)
            || string.IsNullOrWhiteSpace(idempotencyKeyHeader))
        {
            return BadRequest(new { error = "Idempotency-Key header is required." });
        }

        if (!Request.Headers.TryGetValue("If-Match", out var ifMatchHeader)
            || string.IsNullOrWhiteSpace(ifMatchHeader)
            || !uint.TryParse(ifMatchHeader.ToString().Trim('"'), out var expectedVersion))
        {
            return BadRequest(new { error = "If-Match header is required for optimistic concurrency." });
        }

        var actorId = GetUserId();

        var result = await _paymentService.RecordPaymentAsync(
            fundId,
            request.DueId,
            request.Amount,
            idempotencyKeyHeader.ToString(),
            expectedVersion,
            actorId,
            request.Description,
            ct);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                "CONCURRENCY_CONFLICT" => Conflict(new { error = result.Error }),
                "NOT_FOUND" => NotFound(new { error = result.Error }),
                "ALREADY_PAID" => UnprocessableEntity(new { error = result.Error }),
                "INVALID_STATE" => UnprocessableEntity(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };

        var data = result.Value!;
        return Ok(new
        {
            transactionId = data.TransactionId,
            dueId = data.DueId,
            amountPaid = data.AmountPaid,
            remainingBalance = data.RemainingBalance,
            newStatus = data.NewStatus
        });
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : Guid.Empty;
    }
}

public record RecordPaymentRequestDto
{
    public Guid DueId { get; init; }
    public decimal Amount { get; init; }
    public string? Description { get; init; }
}
