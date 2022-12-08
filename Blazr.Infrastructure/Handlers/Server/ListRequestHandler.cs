/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

using Blazr.Infrastructure;

namespace Blazr.Core;

public sealed class ListRequestHandler<TDbContext> : IListRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public ListRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

    public async ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new()
    {
        var list = await _getItemsAsync<TRecord>(request);
        var totalCount = await _getCountAsync<TRecord>(request);

        return ListQueryResult<TRecord>.Success(list, totalCount);
    }

    private ValueTask<IEnumerable<TRecord>> _getItemsAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();
        if (request.FilterExpression is not null)
            query = query
                .Where(request.FilterExpression)
                .AsQueryable();

        if (request.SortExpression is not null)

            query = request.SortDescending
                ? query.OrderByDescending(request.SortExpression)
                : query.OrderBy(request.SortExpression);

        if (request.PageSize > 0)
            query = query
                .Skip(request.StartIndex)
                .Take(request.PageSize);

        return ValueTask.FromResult(query.AsEnumerable());
        //return query is IAsyncEnumerable<TRecord>
        //    ? await query.ToListAsync()
        //    : query.ToList();
    }

    private async ValueTask<long> _getCountAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new()
    {
        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();

        if (request.FilterExpression is not null)
            query = query
                .Where(request.FilterExpression)
                .AsQueryable();

        return query is IAsyncEnumerable<TRecord>
            ? await query.CountAsync(request.Cancellation)
            : query.Count();
    }

}