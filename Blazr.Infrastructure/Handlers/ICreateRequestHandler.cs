/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Core;

public interface ICreateRequestHandler
{
    public ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new();
}
