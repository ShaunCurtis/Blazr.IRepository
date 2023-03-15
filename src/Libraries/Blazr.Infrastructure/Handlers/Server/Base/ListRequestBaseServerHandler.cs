/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public sealed class ListRequestBaseServerHandler<TDbContext> : IListRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private readonly IServiceProvider _serviceProvider;
    private ILogger<ListRequestBaseServerHandler<TDbContext>> _logger;

    public ListRequestBaseServerHandler(IDbContextFactory<TDbContext> factory, IServiceProvider serviceProvider, ILogger<ListRequestBaseServerHandler<TDbContext>> logger)
    {
        _factory = factory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest request)
        where TRecord : class, new()
            => _getItemsAsync<TRecord>(request);

    private async ValueTask<ListQueryResult<TRecord>> _getItemsAsync<TRecord>(ListQueryRequest request)
        where TRecord : class, new()
    {
        int count = 0;
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        var sorterProvider = _serviceProvider.GetService<IRecordSorter<TRecord>>();
        var filterProvider = _serviceProvider.GetService<IRecordFilter<TRecord>>();

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();
        if (filterProvider is not null)
            query = filterProvider.AddFilterToQuery(request.Filters, query);

        count = query is IAsyncEnumerable<TRecord>
            ? await query.CountAsync(request.Cancellation)
            : query.Count();

        if (sorterProvider is not null)
            query = sorterProvider.AddSortToQuery(request.SortField, query, request.SortDescending);

        if (request.PageSize > 0)
            query = query
                .Skip(request.StartIndex)
                .Take(request.PageSize);

        var list = query is IAsyncEnumerable<TRecord>
            ? await query.ToListAsync()
            : query.ToList();

        return ListQueryResult<TRecord>.Success(list, count);
    }
}
