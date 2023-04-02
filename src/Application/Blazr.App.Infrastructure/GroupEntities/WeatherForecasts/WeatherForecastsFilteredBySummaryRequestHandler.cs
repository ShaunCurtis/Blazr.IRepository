/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

[ReportHandler("WeatherForecastsFilteredBySummary")]
public sealed class WeatherForecastsFilteredBySummaryRequestHandler<TDbContext>
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private ILogger<WeatherForecastsFilteredBySummaryRequestHandler<TDbContext>> _logger;

    public WeatherForecastsFilteredBySummaryRequestHandler(IDbContextFactory<TDbContext> factory, ILogger<WeatherForecastsFilteredBySummaryRequestHandler<TDbContext>> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public ValueTask<ListQueryResult<WeatherForecast>> ExecuteAsync(IReportRequest req)
        => _getItemsAsync(req);

    private async ValueTask<ListQueryResult<WeatherForecast>> _getItemsAsync(IReportRequest req)
    {
        WeatherForecastsFilteredBySummaryRequest? request = req as WeatherForecastsFilteredBySummaryRequest;
        int count = 0;
        if (request == null)
            throw new DataPipelineException($"No WeatherForecastsFilteredBySummaryRequest provided in {this.GetType().FullName}");

        var sorterProvider = RecordSortHelper.BuildSortExpression<WeatherForecast>(request.SortField);

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<WeatherForecast> query = dbContext.Set<WeatherForecast>();
        if (request.Summary is not null)
            query = query.Where(item => request.Summary.Equals(item.Summary));

        count = query is IAsyncEnumerable<WeatherForecast>
            ? await query.CountAsync(request.CancellationToken)
            : query.Count();

        if (sorterProvider is not null)
            query = request.SortDescending
                ? query.OrderByDescending(sorterProvider)
                : query.OrderBy(sorterProvider);

        if (request.PageSize > 0)
            query = query
                .Skip(request.StartIndex)
                .Take(request.PageSize);

        var list = query is IAsyncEnumerable<WeatherForecast>
            ? await query.ToListAsync()
            : query.ToList();

        return ListQueryResult<WeatherForecast>.Success(list, count);
    }
}
