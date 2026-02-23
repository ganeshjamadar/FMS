using FundManager.Contracts.Events;
using FundManager.FundAdmin.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FundManager.FundAdmin.Infrastructure.Consumers;

/// <summary>
/// Handles DissolutionInitiated event — transitions Fund to Dissolving state,
/// blocking new members, loans, and contributions (FR-081).
/// </summary>
public class FundDissolvingConsumer : IConsumer<DissolutionInitiated>
{
    private readonly FundAdminDbContext _db;

    public FundDissolvingConsumer(FundAdminDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<DissolutionInitiated> context)
    {
        var evt = context.Message;
        var fund = await _db.Funds.FirstOrDefaultAsync(f => f.Id == evt.FundId, context.CancellationToken);

        if (fund is null) return;

        var result = fund.InitiateDissolution();
        if (result.IsSuccess)
        {
            await _db.SaveChangesAsync(context.CancellationToken);
        }
    }
}

/// <summary>
/// Handles DissolutionConfirmed event — transitions Fund to Dissolved state (terminal).
/// </summary>
public class FundDissolvedConsumer : IConsumer<DissolutionConfirmed>
{
    private readonly FundAdminDbContext _db;

    public FundDissolvedConsumer(FundAdminDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<DissolutionConfirmed> context)
    {
        var evt = context.Message;
        var fund = await _db.Funds.FirstOrDefaultAsync(f => f.Id == evt.FundId, context.CancellationToken);

        if (fund is null) return;

        var result = fund.ConfirmDissolution();
        if (result.IsSuccess)
        {
            await _db.SaveChangesAsync(context.CancellationToken);
        }
    }
}
