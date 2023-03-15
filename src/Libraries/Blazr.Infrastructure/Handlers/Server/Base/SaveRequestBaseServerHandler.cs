/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.Infrastructure;

public sealed class SaveRequestBaseServerHandler<TDbContext>
    : ISaveRequestHandler
    where TDbContext : DbContext
{
    private ILogger<SaveRequestBaseServerHandler<TDbContext>> _logger;

    public SaveRequestBaseServerHandler(ILogger<SaveRequestBaseServerHandler<TDbContext>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        await Task.Yield();
        throw new DataPipelineException($"There's no base implementation of Save defined in {this.GetType().FullName}");
    }
}
