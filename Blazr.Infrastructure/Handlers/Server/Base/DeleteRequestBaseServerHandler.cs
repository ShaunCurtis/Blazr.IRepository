/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public sealed class DeleteRequestBaseServerHandler<TDbContext>
    : IDeleteRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private ILogger<DeleteRequestBaseServerHandler<TDbContext>> _logger;

    public DeleteRequestBaseServerHandler(IDbContextFactory<TDbContext> factory, ILogger<DeleteRequestBaseServerHandler<TDbContext>> logger)
    {
        _logger = logger;
        _factory = factory;
    }

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        using var dbContext = _factory.CreateDbContext();

        dbContext.Remove<TRecord>(request.Item);

        var recordsChanged = await dbContext.SaveChangesAsync(request.Cancellation);

        if (recordsChanged != 1)
            _logger.LogCritical($"{this.GetType().Name} failed to delete the Record.  The returned update count was {recordsChanged}");

        return recordsChanged == 1
            ? CommandResult.Success("Record Deleted")
            : CommandResult.Failure("Error deleting Record");
    }
}