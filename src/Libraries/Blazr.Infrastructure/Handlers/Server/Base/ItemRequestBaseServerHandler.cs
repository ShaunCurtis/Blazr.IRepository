/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using Blazr.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Blazr.Core;

public sealed class ItemRequestBaseServerHandler<TDbContext>
    : IItemRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private ILogger<ItemRequestBaseServerHandler<TDbContext>> _logger;

    public ItemRequestBaseServerHandler(IDbContextFactory<TDbContext> factory, ILogger<ItemRequestBaseServerHandler<TDbContext>> logger)
    {
        _logger = logger;
        _factory = factory;
    }

    public async ValueTask<ItemQueryResult<TRecord>> ExecuteAsync<TRecord>(ItemQueryRequest request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        TRecord? record = null;

        // first check if the record implements IGuidIdentity.  If so we can do a cast and then do the query via the Uid property directly 
        if ((new TRecord()) is IGuidIdentity)
            record = await dbContext.Set<TRecord>().SingleOrDefaultAsync(item => ((IGuidIdentity)item).Uid == request.Uid, request.Cancellation);

        // Try and use the EF FindAsync implementation
        if (record is null)
            record = await dbContext.FindAsync<TRecord>(request.Uid);

        if (record is null) {
            _logger.LogCritical($"{this.GetType().Name} failed to find the Record with Uid: {request.Uid}");
            return ItemQueryResult<TRecord>.Failure("No record retrieved");
    }
        return ItemQueryResult<TRecord>.Success(record);
    }
}
