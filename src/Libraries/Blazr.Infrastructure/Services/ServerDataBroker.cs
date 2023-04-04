/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Infrastructure;

public sealed class RepositoryDataBroker : IDataBroker
{
    private readonly IListRequestHandler _listRequestHandler;
    private readonly IItemRequestHandler _itemRequestHandler;
    private readonly IUpdateRequestHandler _updateRequestHandler;
    private readonly ICreateRequestHandler _createRequestHandler;
    private readonly IDeleteRequestHandler _deleteRequestHandler;

    public RepositoryDataBroker(
        IListRequestHandler listRequestHandler,
        IItemRequestHandler itemRequestHandler,
        ICreateRequestHandler createRequestHandler,
        IUpdateRequestHandler updateRequestHandler,
        IDeleteRequestHandler deleteRequestHandler)
    {
        _listRequestHandler = listRequestHandler;
        _itemRequestHandler = itemRequestHandler;
        _createRequestHandler = createRequestHandler;
        _updateRequestHandler = updateRequestHandler;
        _deleteRequestHandler = deleteRequestHandler;
    }

    public ValueTask<ItemQueryResult<TRecord>> GetItemAsync<TRecord>(ItemQueryRequest request) where TRecord : class, new()
        => _itemRequestHandler.ExecuteAsync<TRecord>(request);

    public ValueTask<ListQueryResult<TRecord>> GetItemsAsync<TRecord>(ListQueryRequest request) where TRecord : class, new()
        => _listRequestHandler.ExecuteAsync<TRecord>(request);

    public ValueTask<CommandResult> CreateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new()
        => _createRequestHandler.ExecuteAsync<TRecord>(request);

    public ValueTask<CommandResult> UpdateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new()
        => _updateRequestHandler.ExecuteAsync<TRecord>(request);

    public ValueTask<CommandResult> DeleteItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new()
        => _deleteRequestHandler.ExecuteAsync<TRecord>(request);
}