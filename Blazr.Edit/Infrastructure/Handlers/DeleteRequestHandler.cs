/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using Microsoft.EntityFrameworkCore;

namespace Blazr.Core;

public class DeleteRequestHandler<TDbContext>
    : IDeleteRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public DeleteRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        using var dbContext = _factory.CreateDbContext();

        dbContext.Remove<TRecord>(request.Item);
        return await dbContext.SaveChangesAsync() == 1
            ? CommandResult.Success("Record Deleted")
            : CommandResult.Failure("Error deleting Record");
    }
}