/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public sealed class UpdateRequestBaseServerHandler<TDbContext>
    : IUpdateRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private ILogger<SaveRequestBaseServerHandler<TDbContext>> _logger;

    public UpdateRequestBaseServerHandler(IDbContextFactory<TDbContext> factory, ILogger<SaveRequestBaseServerHandler<TDbContext>> logger)
    {
        _logger = logger;
        _factory = factory;
    }

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No CommandRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();

        dbContext.Update<TRecord>(request.Item);

        var recordsUpdated = await dbContext.SaveChangesAsync(request.Cancellation);

        if (recordsUpdated != 1)
            _logger.LogCritical($"{this.GetType().Name} failed to Update the Record.  The returned update count was {recordsUpdated}");

        return recordsUpdated == 1
            ? CommandResult.Success("Record Saved")
            : CommandResult.Failure("Error saving Record");
    }
}