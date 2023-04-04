/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Core;

public interface IReportHandler<TReportRequest, TRecord>
    where TReportRequest : IReportRequest
    where TRecord : class, new()
{
    public ValueTask<ListQueryResult<TRecord>> ExecuteAsync(IReportRequest request);
}
