/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public sealed class CreateRequestBaseServerHandler<TDbContext>
    : ICreateRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private ILogger<CreateRequestBaseServerHandler<TDbContext>> _logger;

    public CreateRequestBaseServerHandler(IDbContextFactory<TDbContext> factory, ILogger<CreateRequestBaseServerHandler<TDbContext>> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No CommandRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();

        dbContext.Add<TRecord>(request.Item);

        var recordsChanged = await dbContext.SaveChangesAsync(request.Cancellation);

        if (recordsChanged != 1)
            _logger.LogCritical($"{this.GetType().Name} failed to create the Record.  The returned update count was {recordsChanged}");

        return recordsChanged == 1
            ? CommandResult.Success("Record Updated")
            : CommandResult.Failure("Error updating Record");
    }
}
