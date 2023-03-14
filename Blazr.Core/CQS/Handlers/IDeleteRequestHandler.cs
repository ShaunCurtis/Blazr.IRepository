/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Core;

public interface IDeleteRequestHandler
{
    public ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new();
}

public interface IDeleteRequestHandler<TRecord>
        where TRecord : class, new()
{
    public ValueTask<CommandResult> ExecuteAsync(CommandRequest<TRecord> request);
}
