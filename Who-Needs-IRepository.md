# Are the IRepository and Unit of Work Patterns Redundant?

*IRepository*, combined with the *Unit of Work* pattern, is close to being the *defacto* standard pattern for database access in small to medium projects.  In the DotNetCore context, it's almost always implementated over Entity Framework[EF].  

This article examines why in many cases this adds unnecessary layers of code, complexity and repetition with no benefits.

## Why?

Consider the following fairly classic `IRepository` interface.

```csharp
public interface IRepository<T> 
{        
    public IEnumerable<T> Get();        
    public T GetByID(int Id);        
    public void Create(T record);        
    public void Delete(T record);        
    public void Update(T record);        
}
```

And implementation.

```csharp
public class WeatherForecastRepository<WeatherForecast> 
{        
    public IEnumerable<WeatherForecast> Get();        
    public WeatherForecast GetByID(int Id);        
    public void Create(WeatherForecast record);        
    public void Delete(WeatherForecast record);        
    public void Update(WeatherForecast record);        
}
```

Then consider:

```csharp
public DbSet<WeatherForecast> WeatherForecast { get; set; }
```

You have:

1. WeatherForecast.Add(record);
1. WeatherForecast.Update(record);
1. WeatherForecast.Remove(record);
1. WeatherForecast.Update(record);
1. WeatherForecast.Find{Async}(id);

And the DbSet itself implements `IQueryable` and thus `IEnumerable` for *get many* operations.

These look similar because `DbSet` implements the `IRepositiory` pattern.

You then implement a *Unit of Work* pattern and get:

```csharp
  private UnitOfWork _unitOfWork;

  public void Create(WeatherForecast weatherForecast)
  {
        _unitOfWork.WeatherForecastRepository.Create(myNewRecord);
        _unitOfWork.Save();
  }
```

Written directly against an `IDbContextFactory`:

```csharp
  private IDbContextFactory _factory;

  public void Create(WeatherForecast weatherForecast)
  {
        using var dbContext = _factory.CreateDbContext();
        dbContext.Add<TRecord>(request.Item);
        var recordsChanged = await dbContext.SaveChanges();
  }
```

### Going Generic

The classic `IRepository` pattern builds a Repository class for each data entity.  The `DbContext` has a `Set<T>` method that returns the `DbSet` object for `T`, and a generic `Add<T>`.

```csharp
public void Create<TRecord>(TRecord record)
{
    using var dbContext = _factory.CreateDbContext();
    // dbContext.Set<TRecord>().Add(record);
    dbContext.Add<TRecord>(record);
    var recordsChanged = await dbContext.SaveChanges();
}
```

### Going Async

Most database activity is now asynchronous.  EF is no exception.

```csharp
public ValueTask CreateAsync<TRecord>(TRecord record)
{
    using var dbContext = _factory.CreateDbContext();
    dbContext.Add<TRecord>(record);
    var recordsChanged = await dbContext.SaveChangesAsync();
}
```

### Collapsing the Data Pipeline

This raises an important question:

> Why shouldn't I just access the `IDbContextFactory` from my presentation or business logic code in the Core Domain?  I'm using an abstraction - the `IDbContextFactory`.

Something like:

```csharp
public sealed class WeatherForecastEditPresenter
{
    private readonly IDbContextFactory<InMemoryWeatherDbContext> _factory;

    public WeatherForecastEditPresenter(IDbContextFactory<InMemoryWeatherDbContext> factory)
        => _factory = factory;

    //....

    public async ValueTask UpdateForecastAsync(WeatherForecast weatherForecast)
    {
        using var dbContext = _factory.CreateDbContext();
        dbContext.Add<WeatherForecast>(weatherForecast);
        var recordsChanged = await dbContext.SaveChangesAsync();

        if (recordsChanged != 1)
        {
            // Do somthing
        }
    }
}
```

There are several reasons why not.

1. You're breaking a core tenet of Clean design - a dependancy on the Infrastructure layer.
1. While `IDbContextFactory` is an interface, it's not an abstraction.  You still need to implement some nitty gritty code infrastructure code in the core domain.
1. Your business/present object will inevitably break the *single responsibility principle* by doing whatever it does AND manage the data pipeline.
1. You can't implement this is a API based data pipeline.
1.  While not impossible, it's much easier to mock an abstraction layer than a `DbSet` for testing.

## The Data Broker

The solution is to build a *shim* to provide the interface between the consumers in the Core domain and the `IDbContextFactory`. 

> A shim is *a washer or thin strip of material used to align parts, make them fit.* 

In software terms, a *shim* is a thin layer of code that connects two domains.  Brokers are shim services: the plugs and sockets between domains.

Let's look in detail at how the data broker is implemented: specifically *Update*.

### IDataBroker and the support Objects

`IDataBroker` defines the contract definition for the data pipeline between the *Core* and *Infrastructure* domains.

```csharp
public interface IDataBroker
{
    public ValueTask<CommandResult> UpdateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
    //....
}
```

This introduces two further design practices: all requests are encapsulated in request objects and all return values are encapsulated in result objects.

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

The server side code implements the *Unit of Work* pattern and deals with all the error and exception nitty gritty.

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

This works for standard simple data classes, but we may need to implement custom code for a complex object such as a shopping basket or invoice.

First, abstract the code above into a base or default handler.


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

The key is to inject the `IServiceProvider` and then manually attempt to get `IUpdateRequestHandler<TRecord>` i.e. a custom handler for `TRecord` registered with the DI Service container.  We can't inject it directly in the constructor because, if it doesn't exist, the service container will throw an exception.  If it exists execute it, if not execute the default handler.  

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
