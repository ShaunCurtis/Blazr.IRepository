/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using Blazr.Infrastructure;

namespace Blazr.Core;

public sealed class UpdateRequestHandler<TDbContext>
    : IUpdateRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public UpdateRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No CommandRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();

        dbContext.Update<TRecord>(request.Item);
        return await dbContext.SaveChangesAsync(request.Cancellation) == 1
            ? CommandResult.Success("Record Saved")
            : CommandResult.Failure("Error saving Record");
    }
}