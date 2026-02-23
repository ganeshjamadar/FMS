namespace FundManager.BuildingBlocks.Domain;

/// <summary>
/// Unit of Work pattern interface.
/// Each service's DbContext implements this.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
