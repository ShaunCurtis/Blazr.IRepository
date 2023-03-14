/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.Infrastructure;

public sealed class ItemRequestServerHandler<TDbContext>
    : IItemRequestHandler
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ItemRequestBaseServerHandler<TDbContext> _baseHandler;

    public ItemRequestServerHandler(IServiceProvider serviceProvider, ItemRequestBaseServerHandler<TDbContext> serverHandler)
    {
        _serviceProvider = serviceProvider;
        _baseHandler = serverHandler;
    }

    public async ValueTask<ItemQueryResult<TRecord>> ExecuteAsync<TRecord>(ItemQueryRequest request)
        where TRecord : class, new()
    {
        // Try and get a registered custom handler
        var _customHandler = _serviceProvider.GetService<IItemRequestHandler<TRecord>>();

        // If we get one then one is registered in DI and we execute it
        if (_customHandler is not null)
            return await _customHandler.ExecuteAsync(request);

        // If there's no custom handler registered we run the base handler
        return await _baseHandler.ExecuteAsync<TRecord>(request);
    }
}
