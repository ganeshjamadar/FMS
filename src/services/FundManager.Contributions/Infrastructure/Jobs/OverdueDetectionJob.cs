using FundManager.Contributions.Domain.Services;
using FundManager.Contributions.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FundManager.Contributions.Infrastructure.Jobs;

/// <summary>
/// Background service that periodically detects overdue contributions.
/// FR-033: Pending → Late after grace period (default 5 days).
/// FR-034: Late/Partial/Pending → Missed at month-end.
/// Runs every hour, checking all active funds.
/// </summary>
public class OverdueDetectionJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private const int DefaultGraceDays = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueDetectionJob> _logger;

    public OverdueDetectionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OverdueDetectionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverdueDetectionJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDetectionCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during overdue detection cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunDetectionCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContributionsDbContext>();
        var overdueService = scope.ServiceProvider.GetRequiredService<OverdueDetectionService>();

        // Get distinct fund IDs that have pending/partial/late dues
        var fundIds = await db.ContributionDues
            .Where(d => d.Status != Domain.Entities.ContributionDueStatus.Paid
                        && d.Status != Domain.Entities.ContributionDueStatus.Missed)
            .Select(d => d.FundId)
            .Distinct()
            .ToListAsync(ct);

        _logger.LogInformation("OverdueDetectionJob checking {Count} funds", fundIds.Count);

        foreach (var fundId in fundIds)
        {
            try
            {
                // FR-033: Mark Late after grace period
                var lateCount = await overdueService.MarkLateAsync(fundId, DefaultGraceDays, ct);
                if (lateCount > 0)
                    _logger.LogInformation("Fund {FundId}: marked {Count} dues as Late", fundId, lateCount);

                // FR-034: Mark Missed for previous month's still-unpaid dues
                var previousMonth = GetPreviousMonthYear();
                var missedCount = await overdueService.MarkMissedAsync(fundId, previousMonth, ct);
                if (missedCount > 0)
                    _logger.LogInformation("Fund {FundId}: marked {Count} dues as Missed for {Month}", fundId, missedCount, previousMonth);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing overdue detection for fund {FundId}", fundId);
            }
        }
    }

    private static int GetPreviousMonthYear()
    {
        var now = DateTime.UtcNow;
        var prev = now.AddMonths(-1);
        return prev.Year * 100 + prev.Month;
    }
}
