/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Core;

public interface IReportBroker
{
    public ValueTask<ListQueryResult<TRecord>> GetReportAsync<TReportRequest, TRecord>(IReportRequest reportRequest)
    where TReportRequest : IReportRequest
    where TRecord : class, new();
}