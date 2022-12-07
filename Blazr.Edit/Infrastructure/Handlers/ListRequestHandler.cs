/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

using Blazr.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Blazr.Core;

public class ListRequestHandler<TDbContext> : IListRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public ListRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

    public async ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest request)
        where TRecord : class, new()
    {
        var list = await _getItemsAsync<TRecord>(request);
        var totalCount = await _getCountAsync<TRecord>();

        return ListQueryResult<TRecord>.Success(list, totalCount);
    }

    private async ValueTask<IEnumerable<TRecord>> _getItemsAsync<TRecord>(ListQueryRequest request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();

        if (request.PageSize > 0)
            query = query
                .Skip(request.StartIndex)
                .Take(request.PageSize);

        return query is IAsyncEnumerable<TRecord>
            ? await query.ToListAsync()
            : query.ToList();
    }

    private async ValueTask<long> _getCountAsync<TRecord>()
        where TRecord : class, new()
    {
        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();

        return query is IAsyncEnumerable<TRecord>
            ? await query.CountAsync()
            : query.Count();
    }

}