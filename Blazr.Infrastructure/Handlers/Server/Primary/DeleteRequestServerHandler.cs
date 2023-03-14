/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public sealed class DeleteRequestServerHandler<TDbContext>
    : IDeleteRequestHandler
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DeleteRequestBaseServerHandler<TDbContext> _baseHandler;

    public DeleteRequestServerHandler( IServiceProvider serviceProvider, DeleteRequestBaseServerHandler<TDbContext> baseHandler )
    {
        _serviceProvider = serviceProvider;
        _baseHandler = baseHandler;
    }

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        // Try and get a registerted custom handler
        var _customHandler = ActivatorUtilities.GetServiceOrCreateInstance<IDeleteRequestHandler<TRecord>>(_serviceProvider);

        // If we get one then one is registered in DI and we execute it
        if (_customHandler is not null)
            return await _customHandler.ExecuteAsync(request);

        // If there's no custom handler registered we run the base handler
        return await _baseHandler.ExecuteAsync<TRecord>(request);
    }
}