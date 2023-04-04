/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public sealed class ServerReportBroker
{
    private readonly ILogger _logger;
    private IServiceProvider _serviceProvider;
    public ServerReportBroker(ILogger logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async ValueTask<ListQueryResult<TRecord>> GetReportAsync<TReportRequest, TRecord>(IReportRequest reportRequest)
        where TReportRequest : IReportRequest
        where TRecord : class, new()
    {
        var report = ActivatorUtilities.CreateInstance<IReportHandler<TReportRequest, TRecord>>(_serviceProvider);

        if (report is null)
        {
            var error = ($"A report for {reportRequest.GetType()} is not defined in the Services Container.");
            _logger.LogError(error);
            return ListQueryResult<TRecord>.Failure(error);
        }
        return await report.ExecuteAsync(reportRequest);
    }
}
