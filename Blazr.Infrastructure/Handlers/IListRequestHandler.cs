/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public interface IListRequestHandler
{
    public ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new();
}
