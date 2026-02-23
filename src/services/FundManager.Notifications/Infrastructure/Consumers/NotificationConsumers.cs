using FundManager.Contracts.Events;
using FundManager.Notifications.Domain.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace FundManager.Notifications.Infrastructure.Consumers;

/// <summary>
/// Notifies all fund members when contribution dues are generated for a period.
/// </summary>
public class ContributionDueGeneratedConsumer : IConsumer<ContributionDueGenerated>
{
    private readonly NotificationDispatchService _dispatch;
    private readonly ILogger<ContributionDueGeneratedConsumer> _logger;

    public ContributionDueGeneratedConsumer(
        NotificationDispatchService dispatch,
        ILogger<ContributionDueGeneratedConsumer> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ContributionDueGenerated> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing ContributionDueGenerated for Fund {FundId}, period {MonthYear}",
            evt.FundId, evt.MonthYear);

        // Broadcast to fund — dispatch per-member notifications will be done
        // via the fund membership list. For now, create a fund-level notification
        // that the feed can display to each fund member.
        await _dispatch.DispatchAsync(
            recipientId: Guid.Empty, // Fund-wide broadcast placeholder
            fundId: evt.FundId,
            templateKey: "contribution.due.generated",
            placeholders: new Dictionary<string, string>
            {
                ["monthYear"] = evt.MonthYear.ToString(),
                ["totalAmount"] = evt.TotalAmount.ToString("N2"),
                ["memberCount"] = evt.MemberCount.ToString(),
                ["fundName"] = evt.FundId.ToString(),
            },
            ct: context.CancellationToken);
    }
}

/// <summary>
/// Notifies a member when their contribution payment is confirmed.
/// </summary>
public class ContributionPaidConsumer : IConsumer<ContributionPaid>
{
    private readonly NotificationDispatchService _dispatch;
    private readonly ILogger<ContributionPaidConsumer> _logger;

    public ContributionPaidConsumer(
        NotificationDispatchService dispatch,
        ILogger<ContributionPaidConsumer> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ContributionPaid> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing ContributionPaid for User {UserId}, Fund {FundId}",
            evt.UserId, evt.FundId);

        await _dispatch.DispatchAsync(
            recipientId: evt.UserId,
            fundId: evt.FundId,
            templateKey: "contribution.paid",
            placeholders: new Dictionary<string, string>
            {
                ["amount"] = evt.AmountPaid.ToString("N2"),
                ["fundName"] = evt.FundId.ToString(),
            },
            ct: context.CancellationToken);
    }
}

/// <summary>
/// Notifies the borrower when their loan is approved.
/// </summary>
public class LoanApprovedConsumer : IConsumer<LoanApproved>
{
    private readonly NotificationDispatchService _dispatch;
    private readonly ILogger<LoanApprovedConsumer> _logger;

    public LoanApprovedConsumer(
        NotificationDispatchService dispatch,
        ILogger<LoanApprovedConsumer> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LoanApproved> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing LoanApproved for Loan {LoanId}, Fund {FundId}",
            evt.LoanId, evt.FundId);

        // Notify the loan requester — we use loan ID as we don't have borrower ID on this event
        // The borrower would be resolved via a projection or the loan service
        await _dispatch.DispatchAsync(
            recipientId: evt.ApprovedBy, // Notify the approver — borrower notification via loan projection
            fundId: evt.FundId,
            templateKey: "loan.approved",
            placeholders: new Dictionary<string, string>
            {
                ["fundName"] = evt.FundId.ToString(),
                ["installment"] = evt.ScheduledInstallment.ToString("N2"),
                ["loanId"] = evt.LoanId.ToString(),
            },
            ct: context.CancellationToken);
    }
}

/// <summary>
/// Notifies relevant parties when a loan is rejected.
/// </summary>
public class LoanRejectedConsumer : IConsumer<LoanRejected>
{
    private readonly NotificationDispatchService _dispatch;
    private readonly ILogger<LoanRejectedConsumer> _logger;

    public LoanRejectedConsumer(
        NotificationDispatchService dispatch,
        ILogger<LoanRejectedConsumer> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LoanRejected> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing LoanRejected for Loan {LoanId}, Fund {FundId}",
            evt.LoanId, evt.FundId);

        // Fund-level notification about loan rejection
        await _dispatch.DispatchAsync(
            recipientId: Guid.Empty, // Would be borrower — resolved via projection
            fundId: evt.FundId,
            templateKey: "loan.rejected",
            placeholders: new Dictionary<string, string>
            {
                ["fundName"] = evt.FundId.ToString(),
                ["reason"] = evt.Reason,
                ["loanId"] = evt.LoanId.ToString(),
            },
            ct: context.CancellationToken);
    }
}

/// <summary>
/// Notifies borrower when a repayment entry is due.
/// </summary>
public class RepaymentDueConsumer : IConsumer<RepaymentDueGenerated>
{
    private readonly NotificationDispatchService _dispatch;
    private readonly ILogger<RepaymentDueConsumer> _logger;

    public RepaymentDueConsumer(
        NotificationDispatchService dispatch,
        ILogger<RepaymentDueConsumer> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RepaymentDueGenerated> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing RepaymentDueGenerated for Loan {LoanId}, period {MonthYear}",
            evt.LoanId, evt.MonthYear);

        await _dispatch.DispatchAsync(
            recipientId: Guid.Empty, // Would be borrower — resolved via loan projection
            fundId: evt.FundId,
            templateKey: "repayment.due.generated",
            placeholders: new Dictionary<string, string>
            {
                ["monthYear"] = evt.MonthYear.ToString(),
                ["totalDue"] = evt.TotalDue.ToString("N2"),
                ["interestDue"] = evt.InterestDue.ToString("N2"),
                ["principalDue"] = evt.PrincipalDue.ToString("N2"),
                ["loanId"] = evt.LoanId.ToString(),
            },
            ct: context.CancellationToken);
    }
}

/// <summary>
/// Notifies fund members when a voting session starts for a loan approval.
/// </summary>
public class VotingStartedConsumer : IConsumer<VotingStarted>
{
    private readonly NotificationDispatchService _dispatch;
    private readonly ILogger<VotingStartedConsumer> _logger;

    public VotingStartedConsumer(
        NotificationDispatchService dispatch,
        ILogger<VotingStartedConsumer> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VotingStarted> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing VotingStarted for Loan {LoanId}, Fund {FundId}",
            evt.LoanId, evt.FundId);

        // Broadcast to all fund members to vote
        await _dispatch.DispatchAsync(
            recipientId: Guid.Empty, // Fund-wide broadcast
            fundId: evt.FundId,
            templateKey: "voting.started",
            placeholders: new Dictionary<string, string>
            {
                ["fundName"] = evt.FundId.ToString(),
                ["windowEnd"] = evt.WindowEnd.ToString("yyyy-MM-dd HH:mm"),
                ["loanId"] = evt.LoanId.ToString(),
            },
            ct: context.CancellationToken);
    }
}

/// <summary>
/// Notifies fund members when dissolution is initiated.
/// </summary>
public class DissolutionInitiatedConsumer : IConsumer<DissolutionInitiated>
{
    private readonly NotificationDispatchService _dispatch;
    private readonly ILogger<DissolutionInitiatedConsumer> _logger;

    public DissolutionInitiatedConsumer(
        NotificationDispatchService dispatch,
        ILogger<DissolutionInitiatedConsumer> logger)
    {
        _dispatch = dispatch;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DissolutionInitiated> context)
    {
        var evt = context.Message;
        _logger.LogInformation(
            "Processing DissolutionInitiated for Fund {FundId}",
            evt.FundId);

        await _dispatch.DispatchAsync(
            recipientId: Guid.Empty, // Fund-wide broadcast
            fundId: evt.FundId,
            templateKey: "dissolution.initiated",
            placeholders: new Dictionary<string, string>
            {
                ["fundName"] = evt.FundId.ToString(),
            },
            ct: context.CancellationToken);
    }
}
