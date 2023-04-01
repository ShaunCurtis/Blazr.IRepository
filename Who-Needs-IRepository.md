# Do I Really Need the IRepository and Unit of Work Patterns

IRepository has come close to being the standard pattern in database access in small to medium projects.  Combined with the *Unit of Work* pattern, it's seen as an ideal solution to abstract data access and persistance from the core application code.

In DotNetCore solutions it nearly always implemented over Entity Framework[EF].  What at first seems logical [and good practice], turns out to be illogical on closer inspection.  It wasn't always so: it was a logical approach when using early versions of EF.  Today EF implements those patterns itself, so implementing basically the same pattern twice is not productive.

Consider:

```csharp
public DbSet<WeatherForecast> WeatherForecast { get; set; }
```

`DbSet` is a Repository.  It implements *CRUD* and list operations on the `WeatherForecast` record.

You have:

1. dbContext.Add<TRecord>(record);
1. dbContext.Update<TRecord>(record);
1. dbContext.Remove<TRecord>(record);
1. dbContext.Update<TRecord>(record);
1. dbContext.Find{Async}<TRecord>(id);

And the DbSet itself implements `IQueryable` and thus `IEnumerable` for *get many* operations.

Add the `DbContextFactory` and you have Unit of Work:

```csharp
using var dbContext = _factory.CreateDbContext();
dbContext.Add<TRecord>(record);
var recordsChanged = await dbContext.SaveChangesAsync(cancellationToken);
```

This raises an important question:

> Why shouldn't I just access the `IDbContextFactory` from my presentation or business logic code in the Core Domain?  I'm using an abstraction - the `IDbContextFactory`.

There are three primary reasons why not.

1. You're breaking a core tenet of Clean design - a dependancy on the Infrastructure layer.
2. While `IDbContextFactory` is an interface, it's not an abstraction.  You still need to implement some nitty gritty code infrastructure code in the core domain.
3. Your business/present object will inevitably break the *single responsibility principle* by doing whatever it does AND manage the data pipeline.
4.  While not impossible, it's much easier to mock an abstrsction layer than a `DbSet` for testing.

The solution is to build a *shim* to provide the interface between the consumers in the Core domain and the `IDbContextFactory`. 

> A shim is *a washer or thin strip of material used to align parts, make them fit.* 

In software terms, a *shim* is a thin layer of code that connects two domains.  We'll call our shim a broker.

Let's look in detail at how we implement this, and specifically at *Update*.

An `IDataBroker` interface provides the abstraction between the domains by defining a contract definition for the data pipeline.

```csharp
public interface IDataBroker
{
    public ValueTask<CommandResult> UpdateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
    //....
}
```

The `CommandRequest` looks like this:

```csharp
public sealed record CommandRequest<TRecord>
{
    public required TRecord Item { get; init; }
    public CancellationToken Cancellation { get; set; } = new();
}
```

And the CommandResult:

```csharp
public sealed record CommandResult
{
    public bool Successful { get; init; }
    public string Message { get; init; } = string.Empty;
    //....
}
```

We can then create the server side implementation.  This implements the Unit of Work pattern and deals with all the error and exception nitty gritty.

```
public sealed class ServerDataBroker : IDataBroker
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private ILogger<SaveRequestBaseServerHandler<TDbContext>> _logger;

    public ServerDataBroker(IDbContextFactory<TDbContext> factory, ILogger<SaveRequestBaseServerHandler<TDbContext>> logger)
    {
        _logger = logger;
        _factory = factory;
    }

    public ValueTask<CommandResult> UpdateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new()
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

    //....
}
```

We can abstract this code further to handle custom updates.

First we abstract the code above into a base or default handler for updates.


```csharp
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
```

And then create the main handler.

The key activity here is we inject the `IServiceProvider` and then manually attempt to get `IUpdateRequestHandler<TRecord>` i.e. a custom hanlder for `TRecord` registered with thw DI Service container.  We don't inject it directly as it may not exist.  If it exists we execute it, if it doesn't we use thw default handler.  

```csharp
public sealed class UpdateRequestServerHandler<TDbContext>
    : IUpdateRequestHandler
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly UpdateRequestBaseServerHandler<TDbContext> _baseHandler;

    public UpdateRequestServerHandler(IServiceProvider serviceProvider, UpdateRequestBaseServerHandler<TDbContext> baseHandler)
    {
        _serviceProvider = serviceProvider;
        _baseHandler = baseHandler;
    }

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        // Try and get a registerted custom handler
        var _customHandler = _serviceProvider.GetService<IUpdateRequestHandler<TRecord>>();

        // If we get one then one is registered in DI and we execute it
        if (_customHandler is not null)
            return await _customHandler.ExecuteAsync(request);

        // If there's no custom handler registered we run the base handler
        return await _baseHandler.ExecuteAsync<TRecord>(request);
    }
}
```

Our Server data broker now looks like this:

```
public sealed class ServerDataBroker : IDataBroker
{
    private readonly IUpdateRequestHandler _updateRequestHandler;
 
    public ServerDataBroker(ICreateRequestHandler createRequestHandler)
    {
        _updateRequestHandler = updateRequestHandler;
    }

    public ValueTask<CommandResult> UpdateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new()
        => _updateRequestHandler.ExecuteAsync<TRecord>(request);
}
```
