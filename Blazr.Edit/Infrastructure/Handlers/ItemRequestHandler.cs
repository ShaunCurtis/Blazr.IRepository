/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using Blazr.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Blazr.Core;

public class ItemRequestHandler<TDbContext>
    : IItemRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public ItemRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

    public async ValueTask<ItemQueryResult<TRecord>> ExecuteAsync<TRecord>(ItemQueryRequest request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        TRecord? record = null;

        // first check if the record implements IRecord.  If so we can do a cast and then do the query via the Uid property directly 
        if ((new TRecord()) is IGuidIdentity)
            record = await dbContext.Set<TRecord>().SingleOrDefaultAsync(item => ((IGuidIdentity)item).Uid == request.Uid);

        // Try and use the EF FindAsync implementation
        if (record is null)
            record = await dbContext.FindAsync<TRecord>(request.Uid);

        if (record is null)
            return ItemQueryResult<TRecord>.Failure("No record retrieved");

        return ItemQueryResult<TRecord>.Success(record);
    }
}