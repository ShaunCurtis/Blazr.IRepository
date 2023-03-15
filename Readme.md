# Rethinking the Repository Pattern

> Version 2.0 - revised 15-Mar-2023

## Introduction

This is no regurgitated how to build the classic `IRepository` in DotNetCore.  The implementation presented here is a very different animal.

How so:

1. There's no implementation per entity class.  You won't see this:

```csharp
    public class WeatherForecastRepository : GenericRepository<WeatherForecast>, IWeatherForcastRepository
    {
        public WeatherForecastRepository(DbContextClass dbContext) : base(dbContext) {}
    }

    public interface IProductRepository : IGenericRepository<WeatherForecast> { }
```

2. There's no separate `UnitOfWork` classes: it's built in.

3. All standard Data I/O uses a single Data Broker.

4. CQS practices and patterns creep into the design.
 
5. The implementation supports both server and API infrastructures 

## Nomenclature, Terminology and Practices

 - **DI**: Dependancy Injection 
 - **CQS**: Command/Query Separation

The code is:
 - *Net7.0*
 - C# 10
 - Nullable enabled 

## Repo

The Repo and latest version of this article are here: [Blazr.IRepository](https://github.com/ShaunCurtis/Blazr.IRepository).

## The Data Store

The solution needs a real data store for testing: it implements an Entity Framework In-Memory database.

I'm a Blazor developer so my data class is the good old `WeatherForecast`. The code is in the Appendix.

This is the `DbContext` used by the DBContext factory.

```csharp
public sealed class InMemoryWeatherDbContext : DbContext
{
    public DbSet<WeatherForecast> WeatherForecast { get; set; } = default!;
    public InMemoryWeatherDbContext(DbContextOptions<InMemoryWeatherDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<WeatherForecast>().ToTable("WeatherForecast");
}
```

### Testing the Factory and Context

The following XUnit test demonstrates the basic datastore setup in DI. It:
1. Sets up a DI container
2. Loads the data from the Test Provider
3. Tests the record count is correct
4. Tests one arbitary record is correct.

```csharp
[Fact]
public async Task DBContextTest()
{
    // Gets the control test data
    var testProvider = WeatherTestDataProvider.Instance();

    // Build our services container
    var services = new ServiceCollection();

    // Define the DbSet and Server Type for the DbContext Factory
    services.AddDbContextFactory<InMemoryWeatherDbContext>(options
        => options.UseInMemoryDatabase($"WeatherDatabase-{Guid.NewGuid().ToString()}"));

    var rootProvider = services.BuildServiceProvider();

    //define a scoped container
    var providerScope = rootProvider.CreateScope();
    var provider = providerScope.ServiceProvider;

    // get the DbContext factory and add the test data
    var factory = provider.GetService<IDbContextFactory<InMemoryWeatherDbContext>>();
    if (factory is not null)
        WeatherTestDataProvider.Instance().LoadDbContext<InMemoryWeatherDbContext>(factory);

    // Check the data has been loaded
    var dbContext = factory!.CreateDbContext();
    Assert.NotNull(dbContext);

    var count = dbContext.Set<WeatherForecast>().Count();
    Assert.Equal(testProvider.WeatherForecasts.Count(), count);

    // Test an arbitary record
    var testRecord = testProvider.GetRandomRecord()!;
    var record = await dbContext.Set<WeatherForecast>().SingleOrDefaultAsync(item => item.Uid.Equals(testRecord.Uid));
    Assert.Equal(testRecord, record);

    // Dispose of the resources correctly
    providerScope.Dispose();
    rootProvider.Dispose();
}
```

## The Classic Repository Pattern Implementation

Here's a nice succinct implementation that I found on the Internet.

```csharp
    public abstract class Repository<T> : IRepository<T> where T : class
    {
        protected readonly DbContextClass _dbContext;

        protected GenericRepository(DbContextClass context)
            => _dbContext = context;

        public async Task<T> GetById(int id)
            => await _dbContext.Set<T>().FindAsync(id);

        public async Task<IEnumerable<T>> GetAll()
            => await _dbContext.Set<T>().ToListAsync();

        public async Task Add(T entity)
             => await _dbContext.Set<T>().AddAsync(entity);

        public void Delete(T entity)
            => _dbContext.Set<T>().Remove(entity);

        public void Update(T entity)
           =>  _dbContext.Set<T>().Update(entity);
    }
}
```
Picking it apart:

1. What happens when a `null` is returned, what does it mean?
2. Did that add/update/delete really succeed?  How do I know?
3. How do you handle cancellation tokens?  Most of the async methods now accept a cancellation token.
4. What happens when your `DBSet` contains a million records (maybe the DBA got something wrong last night)?
5. .....

## This Implementation

An alternative to the Repository patttern is CQS.  It's more verbose, but it implements some good practices that we can use in our implementation.

### Requests and Results

Request objects encapulate what we request and the result objects the data and status information we expect back.  Thet are defined as records: defined once and then consumed.

### Commands

A *Command* is a request to make a change to the data store: Create/Update/Delete operations.

```csharp
public record CommandRequest<TRecord>
{
    public required TRecord Item { get; init; }
    public CancellationToken Cancellation { get; set; } = new ();
}
```
The result only returns status information: no data.  We can define a result like this:

```csharp
public record CommandResult
{
    public bool Successful { get; init; }
    public string Message { get; init; } = string.Empty;

    private CommandResult() { }

    public static CommandResult Success(string? message = null)
        => new CommandResult { Successful = true, Message= message ?? string.Empty };

    public static CommandResult Failure(string message)
        => new CommandResult { Message = message};
}
```

There's one exception to the return rule: the Id for an inserted record.  If you aren't using Guids to give your records unique identifiers, then the database generated Id should be considered a piece of status information!

### Item Requests

A *Query* is a request to get data from the data store: no mutation.  We can define an item query like this:

```csharp
public sealed record ItemQueryRequest
{
    public required Guid Uid { get; init; }
    public CancellationToken Cancellation { get; set; } = new();
}
```

And the return result: the requested data and status.

```csharp
public sealed record ItemQueryResult<TRecord>
{
    public TRecord? Item { get; init;} 
    public bool Successful { get; init; }
    public string Message { get; init; } = string.Empty;

    private ItemQueryResult() { }

    public static ItemQueryResult<TRecord> Success(TRecord Item, string? message = null)
        => new ItemQueryResult<TRecord> { Successful=true, Item= Item, Message= message ?? string.Empty };

    public static ItemQueryResult<TRecord> Failure(string message)
        => new ItemQueryResult<TRecord> { Message = message};
}
```

### List Queries

List queries present a few extra challenges.

1. They should never request everything.  In edge conditions there may be 1,000,000+ rows in a table.  Every request should be constrained.  The request defines `StartIndex` and `PageSize` to both constrain the data and provide paging.  If you set the page size to 1,000,000, will your data pipeline and front end handle it gracefully?
2. They need to handle sorting and filtering.
   
Sorting is relatively simple.  Provide a property name that can be resolved by Reflection and a direction.

Filtering is messy.  The ideal solution would use Linq Expressions.  But they can't be passed over an API call.  This solution uses `FilterDefinition` colection to define the filters.  The `FilterDefinition` defines a named expression and a string containing a simnple value or Json object that the receiver knows how to resolve the name to a `Expression` and how to apply the provided data in the expression.  We'll see the process in action later.   

```csharp
public sealed record ListQueryRequest
{
    public int StartIndex { get; init; } = 0;
    public int PageSize { get; init; } = 1000;
    public CancellationToken Cancellation { get; set; } = new ();
    public bool SortDescending { get; } = false;
    public IEnumerable<FilterDefinition> Filters { get; init; } = Enumerable.Empty<FilterDefinition>();
    public string SortField { get; init; } = string.Empty;
}
```

`FilterDefinition` is a simple record.

```csharp
public sealed record FilterDefinition(string FilterName, string FilterData);
```

The result returns the items, the total item count (for paging) and status information.  `Items` are always returned as `IEnumerable`.

```csharp
public sealed record ListQueryResult<TRecord>
{
    public IEnumerable<TRecord> Items { get; init;} = Enumerable.Empty<TRecord>();  
    public bool Successful { get; init; }
    public string Message { get; init; } = string.Empty;
    public long TotalCount { get; init; }

    private ListQueryResult() { }

    public static ListQueryResult<TRecord> Success(IEnumerable<TRecord> Items, long totalCount, string? message = null)
        => new ListQueryResult<TRecord> {Successful=true,  Items= Items, TotalCount = totalCount, Message= message ?? string.Empty };

    public static ListQueryResult<TRecord> Failure(string message)
        => new ListQueryResult<TRecord> { Message = message};
}
```

#### Sorting

Define an interface for the sorter:

```csharp
public interface IRecordSorter<TRecord>
    where TRecord : class
{
    public IQueryable<TRecord> AddSortToQuery(string fieldName, IQueryable<TRecord> query, bool sortDescending);
}
```

And a base class to implement common functionality:

```csharp
public class RecordSortBase<TRecord>
    where TRecord : class
{
    protected static IQueryable<TRecord> Sort(IQueryable<TRecord> query, bool sortDescending, Expression<Func<TRecord, object>> sorter)
    {
        return sortDescending
            ? query.OrderByDescending(sorter)
            : query.OrderBy(sorter);
    }

    protected static bool TryBuildSortExpression(string sortField, [NotNullWhen(true)] out Expression<Func<TRecord, object>>? expression)
    {
        expression = null;

        Type recordType = typeof(TRecord);
        PropertyInfo sortProperty = recordType.GetProperty(sortField)!;
        if (sortProperty is null)
            return false;

        ParameterExpression parameterExpression = Expression.Parameter(recordType, "item");
        MemberExpression memberExpression = Expression.Property((Expression)parameterExpression, sortField);
        Expression propertyExpression = Expression.Convert(memberExpression, typeof(object));

        expression = Expression.Lambda<Func<TRecord, object>>(propertyExpression, parameterExpression);

        return true;
    }
}
```

And then the `WeatherForecast` implementation:

```csharp
public class WeatherForecastSorter : RecordSortBase<WeatherForecast>, IRecordSorter<WeatherForecast>
{
    public  IQueryable<WeatherForecast> AddSortToQuery(string fieldName, IQueryable<WeatherForecast> query, bool sortDescending)
        => fieldName switch
        {
            WeatherForecastConstants.TemperatureC => Sort(query, sortDescending, OnTemperature),
            WeatherForecastConstants.Summary => Sort(query, sortDescending, OnSummary),
            _ => Sort(query, sortDescending, OnDate)
        };

    private static Expression<Func<WeatherForecast, object>> OnDate => item => item.Date;
    private static Expression<Func<WeatherForecast, object>> OnTemperature => item => item.TemperatureC;
    private static Expression<Func<WeatherForecast, object>> OnSummary => item => item.Summary ?? string.Empty;
}
```

#### Filtering

An interface. `AddFilterToQuery` adds the filter expression to the provided `IQueryable` instance.

```csharp
public interface IRecordFilter<TRecord>
    where TRecord : class
{
    public IQueryable<TRecord> AddFilterToQuery(IEnumerable<FilterDefinition> filters, IQueryable<TRecord> query);
}
```

And an implementation for `WeatherForecast`.  A switch atatemwent matches named expressions to the actual expressions and applies the provided data.

```csharp
public class WeatherForecastFilter : IRecordFilter<WeatherForecast>
{
    public IQueryable<WeatherForecast> AddFilterToQuery(IEnumerable<FilterDefinition> filters, IQueryable<WeatherForecast> query)
    {
        foreach (var filter in filters)
        {
            switch (filter.FilterName)
            {
                case WeatherForecastConstants.ByTemperature:
                    if (BindConverter.TryConvertTo<int>(filter.FilterData, null, out int temperatureValue))
                        query = query.Where(ByTemperature(temperatureValue));
                    break;

                case WeatherForecastConstants.BySummary:
                    if (!string.IsNullOrWhiteSpace(filter.FilterData))
                        query = query.Where(BySummary(filter.FilterData));
                    break;

                case WeatherForecastConstants.TemperatureLessThan:
                    if (BindConverter.TryConvertTo<int>(filter.FilterData, null, out int value))
                        query = query.Where(TemperatureLessThan(value));
                    break;

                default:
                    break;
            }
        }

        if (query is IQueryable)
            return query;

        return query.AsQueryable();
    }

    private static Expression<Func<WeatherForecast, bool>> ByTemperature(int value) => item => item.TemperatureC.Equals(value);
    private static Expression<Func<WeatherForecast, bool>> BySummary(string value) => item => item.Summary == value;
    private static Expression<Func<WeatherForecast, bool>> TemperatureLessThan(int value) => item => item.TemperatureC < value;
}
```

You will see these used shortly.  they are registered as transient DI services like this:

```csharp
services.AddTransient<IRecordSorter<WeatherForecast>, WeatherForecastSorter>();
services.AddTransient<IRecordFilter<WeatherForecast>, WeatherForecastFilter>();
```

### Handlers

Handlers are small single purpose classes that handle requests and return results.  They abstract the nitty-gritty execution from the higher level Data Broker.

Each process has at least two handlers.

1. The base handler is a generic handler that implements the transaction against the data source.
2. The primary handler manages which handler is called. If a custom handler is registered in DI for `TRecord` it will execute the custom handler, otherwise it will execute the base handler.
3. The custom handler is a handler registered in DI against a specific record.

#### Command Handlers

The interface provides the abstraction.  There's both a standard and generic implementation.

```csharp
public interface ICreateRequestHandler
{
    public ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new();
}

public interface ICreateRequestHandler<TRecord>
        where TRecord : class, new()
{
    public ValueTask<CommandResult> ExecuteAsync(CommandRequest<TRecord> request);
}

```

The base implementation does the real work.

1. Injects the DBContext Factory.
2. Implements *Unit of Work* Db contexts through the DbContext factory.
3. Uses the Add method on the context to add the record to EF.
4. Calls `SaveChangesAsync`, passing in the Cancellation token, and expects a single change to be reported.
5. Provides status information if things go wrong.
6. Logs information to the coinfigured logger.

```csharp
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
```

And the primary implementation.

1. Injects the IServiceProvider and base handler.
2. Checks to see if a custom handler is registered in DI.
3. If one is it executes it.
4. Otherwise it executes the base handler.


```csharp
public sealed class CreateRequestServerHandler<TDbContext>
    : ICreateRequestHandler
    where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly CreateRequestBaseServerHandler<TDbContext> _baseHandler;

    public CreateRequestServerHandler(IServiceProvider serviceProvider, CreateRequestBaseServerHandler<TDbContext> baseHandler)
    { 
        _serviceProvider = serviceProvider;
        _baseHandler = baseHandler;
    }

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        // Try and get a registerted custom handler
        var _customHandler = _serviceProvider.GetService<ICreateRequestHandler<TRecord>>();

        // If we get one then one is registered in DI and we execute it
        if (_customHandler is not null)
            return await _customHandler.ExecuteAsync(request);

        // If there's no custom handler registered we run the base handler
        return await _baseHandler.ExecuteAsync<TRecord>(request);
    }
}
```

The Update and Delete handlers are the same but use different `dbContext` methods: Update and Remove.

#### Item Request Handler

The interface.

```csharp
public interface IItemRequestHandler
{
    public ValueTask<ItemQueryResult<TRecord>> ExecuteAsync<TRecord>(ItemQueryRequest request)
        where TRecord : class, new();
}

public interface IItemRequestHandler<TRecord>
        where TRecord : class, new()
{
    public ValueTask<ItemQueryResult<TRecord>> ExecuteAsync(ItemQueryRequest request);
}
```

The base implementation. Note:

1. Injects the DBContext Factory.
2. Implements *Unit of Work* Db contexts through the DbContext factory.
3. Turns off tracking.  There's no mutation involved in this transaction.
4. Checks to see if it can use an Id to get the item - the record implements `IGuidIdentity`.
5. If not, tries `FindAsync` which uses the inbuilt `Key` methodology to get the record.
5. Provides status information if things go wrong.


```csharp
public sealed class ItemRequestBaseServerHandler<TDbContext>
    : IItemRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private ILogger<ItemRequestBaseServerHandler<TDbContext>> _logger;

    public ItemRequestBaseServerHandler(IDbContextFactory<TDbContext> factory, ILogger<ItemRequestBaseServerHandler<TDbContext>> logger)
    {
        _logger = logger;
        _factory = factory;
    }

    public async ValueTask<ItemQueryResult<TRecord>> ExecuteAsync<TRecord>(ItemQueryRequest request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        TRecord? record = null;

        // first check if the record implements IGuidIdentity.  If so we can do a cast and then do the query via the Uid property directly 
        if ((new TRecord()) is IGuidIdentity)
            record = await dbContext.Set<TRecord>().SingleOrDefaultAsync(item => ((IGuidIdentity)item).Uid == request.Uid, request.Cancellation);

        // Try and use the EF FindAsync implementation
        if (record is null)
            record = await dbContext.FindAsync<TRecord>(request.Uid);

        if (record is null) {
            _logger.LogCritical($"{this.GetType().Name} failed to find the Record with Uid: {request.Uid}");
            return ItemQueryResult<TRecord>.Failure("No record retrieved");
    }
        return ItemQueryResult<TRecord>.Success(record);
    }
}
```

And the primary handler:

```csharp
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
```


#### List Request Handler

The list request handler is the most complex and relies on quite a lot of infrastructure to operate.

The interface.

```csharp
public interface IListRequestHandler
{
    public ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest request)
        where TRecord : class, new();
}

public interface IListRequestHandler<TRecord>
    where TRecord : class, new()
{
    public ValueTask<ListQueryResult<TRecord>> ExecuteAsync(ListQueryRequest request);
}
```

The base implementation.  There's a lot going on so I'll break down into small sections:

Try and get sort and filter providers from DI for `TRecord`.  They will be null if none are registered.

```csharp
        var sorterProvider = _serviceProvider.GetService<IRecordSorter<TRecord>>();
        var filterProvider = _serviceProvider.GetService<IRecordFilter<TRecord>>();
```

Get a DbContext.  We don't need tracking as this is read only.  Gets the `IQueryable` object that represents the `TRecord`.

```csharp
    using var dbContext = _factory.CreateDbContext();
    dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

    IQueryable<TRecord> query = dbContext.Set<TRecord>();
```

Add the filter to the query if one exists.

```csharp
if (filterProvider is not null)
    query = filterProvider.AddFilterToQuery(request.Filters, query);
```

Get the total filtered record count by running a materialization against the query.  This will get translated into an efficient query against the data source.   The way the query gwets executed depends on whether we are doing any mocking so we test the type before attempting an asynchronous or synchronous call.

```csharp
    count = query is IAsyncEnumerable<TRecord>
        ? await query.CountAsync(request.Cancellation)
        : query.Count();
```

Next we add sorting to the query if a sort provider exists.

```csharp
    if (sorterProvider is not null)
        query = sorterProvider.AddSortToQuery(request.SortField, query, request.SortDescending);
```

And paging.

```csharp
    if (request.PageSize > 0)
        query = query
            .Skip(request.StartIndex)
            .Take(request.PageSize);
```

We then materialize the query to get the data set.

```csharp
    var list = query is IAsyncEnumerable<TRecord>
        ? await query.ToListAsync()
        : query.ToList();
```

And finally return the result.

```csharp
    return ListQueryResult<TRecord>.Success(list, count);
```

The purpoose is to build up the query in the most efficient way, taking advantage of `IQueryable` functionality to only materialize out the minimum data sets.

Here's the full class.

```csharp
public sealed class ListRequestBaseServerHandler<TDbContext> : IListRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;
    private readonly IServiceProvider _serviceProvider;
    private ILogger<ListRequestBaseServerHandler<TDbContext>> _logger;

    public ListRequestBaseServerHandler(IDbContextFactory<TDbContext> factory, IServiceProvider serviceProvider, ILogger<ListRequestBaseServerHandler<TDbContext>> logger)
    {
        _factory = factory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest request)
        where TRecord : class, new()
            => _getItemsAsync<TRecord>(request);

    private async ValueTask<ListQueryResult<TRecord>> _getItemsAsync<TRecord>(ListQueryRequest request)
        where TRecord : class, new()
    {
        int count = 0;
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        var sorterProvider = _serviceProvider.GetService<IRecordSorter<TRecord>>();
        var filterProvider = _serviceProvider.GetService<IRecordFilter<TRecord>>();

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();
        if (filterProvider is not null)
            query = filterProvider.AddFilterToQuery(request.Filters, query);

        count = query is IAsyncEnumerable<TRecord>
            ? await query.CountAsync(request.Cancellation)
            : query.Count();

        if (sorterProvider is not null)
            query = sorterProvider.AddSortToQuery(request.SortField, query, request.SortDescending);

        if (request.PageSize > 0)
            query = query
                .Skip(request.StartIndex)
                .Take(request.PageSize);

        var list = query is IAsyncEnumerable<TRecord>
            ? await query.ToListAsync()
            : query.ToList();

        return ListQueryResult<TRecord>.Success(list, count);
    }
}
```

### The Repository Class Replacement

First the interface.

The very important bit is the generic `TRecord` definition on each method, not on the interface.  This removes the need for entity specific implementations.

```csharp
public interface IDataBroker
{
    public ValueTask<ListQueryResult<TRecord>> GetItemsAsync<TRecord>(ListQueryRequest request) where TRecord : class, new();
    public ValueTask<ItemQueryResult<TRecord>> GetItemAsync<TRecord>(ItemQueryRequest request) where TRecord : class, new();
    public ValueTask<CommandResult> UpdateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
    public ValueTask<CommandResult> CreateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
    public ValueTask<CommandResult> DeleteItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
}
```

And the implementation.  Each handler is registered in DI and injected into the broker.

```csharp
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
```

## Testing the Data Broker

We can now define a set of tests for the data broker.  I've included two here.  The rest are in the Repo.

First a static class to create our root DI container and populate the database.

```csharp
public static class ServiceContainers
{
    public static ServiceProvider BuildRootContainer()
    {
        var services = new ServiceCollection();

        // Define the DbSet and Server Type for the DbContext Factory
        services.AddDbContextFactory<InMemoryWeatherDbContext>(options
            => options.UseInMemoryDatabase($"WeatherDatabase-{Guid.NewGuid().ToString()}"));
        // Define the Broker and Handlers
        services.AddScoped<IDataBroker, RepositoryDataBroker>();
        
        // Add the Standard Handlers
        services.AddScoped<IListRequestHandler, ListRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IItemRequestHandler, ItemRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IUpdateRequestHandler, UpdateRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<ICreateRequestHandler, CreateRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IDeleteRequestHandler, DeleteRequestServerHandler<InMemoryWeatherDbContext>>();

        // Add the Base Handlers
        services.AddScoped<ListRequestBaseServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<ItemRequestBaseServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<UpdateRequestBaseServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<CreateRequestBaseServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<DeleteRequestBaseServerHandler<InMemoryWeatherDbContext>>();

        // Add the custom sort and filter proividers
        services.AddTransient<IRecordSorter<WeatherForecast>, WeatherForecastSorter>();
        services.AddTransient<IRecordFilter<WeatherForecast>, WeatherForecastFilter>();

        //Define the ILogger
        services.AddLogging(builder => builder.AddDebug());

        // Create the container
        return services.BuildServiceProvider();
    }

    public static IDbContextFactory<InMemoryWeatherDbContext> GetPopulatedFactory(IServiceProvider provider)
    {
        // get the DbContext factory and add the test data
        var factory = provider.GetService<IDbContextFactory<InMemoryWeatherDbContext>>();
        if (factory is not null)
            WeatherTestDataProvider.Instance().LoadDbContext<InMemoryWeatherDbContext>(factory);

        return factory!;
    }
}
```

The GetItems test:

```csharp
[Fact]
public async Task GetItemsTest()
{
    // Get our test provider to use as our control
    var testProvider = WeatherTestDataProvider.Instance();

    // Build the root DI Container
    var rootProvider = ServiceContainers.BuildRootContainer();

    //define a scoped container
    var providerScope = rootProvider.CreateScope();
    var provider = providerScope.ServiceProvider;

    // get the DbContext factory and add the test data
    var factory = ServiceContainers.GetPopulatedFactory(provider);

    // Check we can retrieve thw first 1000 records
    var dbContext = factory!.CreateDbContext();
    Assert.NotNull(dbContext);

    var databroker = provider.GetRequiredService<IDataBroker>();

    var request = new ListQueryRequest();
    var result = await databroker.GetItemsAsync<WeatherForecast>(request);

    var expectedCount = testProvider.WeatherForecasts.Count();

    Assert.NotNull(result);
    Assert.Equal(expectedCount, result.TotalCount);

    providerScope.Dispose();
    rootProvider.Dispose();
}
```
The Add Item test:

```csharp
[Fact]
public async Task AddItemTest()
{
    // Get our test provider to use as our control
    var testProvider = WeatherTestDataProvider.Instance();

    // Build the root DI Container
    var rootProvider = ServiceContainers.BuildRootContainer();

    //define a scoped container
    var providerScope = rootProvider.CreateScope();
    var provider = providerScope.ServiceProvider;

    // get the DbContext factory and add the test data
    var factory = ServiceContainers.GetPopulatedFactory(provider);

    // Check we can retrieve thw first 1000 records
    var dbContext = factory!.CreateDbContext();
    Assert.NotNull(dbContext);

    var databroker = provider.GetRequiredService<IDataBroker>();

    // Create a Test record
    var newRecord = new WeatherForecast { Uid = Guid.NewGuid(), Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 50, Summary = "Add Testing" };

    // Add the Record
    {
        var request = new CommandRequest<WeatherForecast>() { Item = newRecord };
        var result = await databroker.CreateItemAsync<WeatherForecast>(request);

        Assert.NotNull(result);
        Assert.True(result.Successful);
    }
}
```

## Wrapping Up

What I've presented here is a hybrid Repository Pattern.  It maintains the Repository Pattern's simplicity, and adds some of the best CQS Pattern features.  

Abstracting the nitty-gritty EF and Linq code to individual handlers keeps the classes small, succinct and single purpose.

The single Data Broker simplifies data pipeline configuration for the Core and Presentation domains.

To those who believe that implementing any database pipeline over EF is an anti-pattern, my answer is simple.  

I use EF as just another Object Request Broker [ORB].  You can plug this pipeline into Dapper, LinqToDb, ... .  I never build core business logic code (data relationships) into my Data/Infrastructure Domain: [personal view] crazy idea.

## Appendix

### The Data Store

The test system implements an Entity Framework In-Memory database.

I'm a Blazor developer so naturally my demo data class is `WeatherForecast`.  Here's my data class.  Note it is a record for immutability and I've set some arbitary default values for testing purposes.

```csharp
public sealed record WeatherForecast : IGuidIdentity
{
    [Key] public Guid Uid { get; init; } = Guid.Empty;
    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.Now);
    public int TemperatureC { get; init; } = 60;
    public string? Summary { get; init; } = "Testing";
}
```

First a class to generate a data set.  This is a *Singleton* pattern class (not a DI singleton).  Methods such as `GetRandomRecord` are for testing.

```csharp
public sealed class WeatherTestDataProvider
{
    private int RecordsToGenerate;

    public IEnumerable<WeatherForecast> WeatherForecasts { get; private set; } = Enumerable.Empty<WeatherForecast>();

    private WeatherTestDataProvider()
        => this.Load();

    public void LoadDbContext<TDbContext>(IDbContextFactory<TDbContext> factory) where TDbContext : DbContext
    {
        using var dbContext = factory.CreateDbContext();

        var weatherForcasts = dbContext.Set<WeatherForecast>();

        // Check if we already have a full data set
        // If not clear down any existing data and start again
        if (weatherForcasts.Count() == 0)
        {
            dbContext.AddRange(this.WeatherForecasts);
            dbContext.SaveChanges();
        }
    }

    public void Load(int records = 100)
    {
        RecordsToGenerate = records;

        if (WeatherForecasts.Count() == 0)
            this.LoadForecasts();
    }

    private void LoadForecasts()
    {
        var forecasts = new List<WeatherForecast>();

        for (var index = 0; index < RecordsToGenerate; index++)
        {
            var rec = new WeatherForecast
            {
                Uid = Guid.NewGuid(),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)],
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
            };
            forecasts.Add(rec);
        }

        this.WeatherForecasts = forecasts;
    }

    public WeatherForecast GetForecast()
    {
        return new WeatherForecast
        {
            Uid = Guid.NewGuid(),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)],
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
            TemperatureC = Random.Shared.Next(-20, 55),
        };
    }

    public WeatherForecast? GetRandomRecord()
    {
        var record = new WeatherForecast();
        if (this.WeatherForecasts.Count() > 0)
        {
            var ran = new Random().Next(0, WeatherForecasts.Count());
            return this.WeatherForecasts.Skip(ran).FirstOrDefault();
        }
        return null;
    }

    private static WeatherTestDataProvider? _weatherTestData;

    public static WeatherTestDataProvider Instance()
    {
        if (_weatherTestData is null)
            _weatherTestData = new WeatherTestDataProvider();

        return _weatherTestData;
    }

    public static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

}
```

The `DbContext`.

```csharp
public sealed class InMemoryWeatherDbContext
    : DbContext
{
    public DbSet<WeatherForecast> WeatherForecast { get; set; } = default!;
    public InMemoryWeatherDbContext(DbContextOptions<InMemoryWeatherDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<WeatherForecast>().ToTable("WeatherForecast");
}
```

## Updates

### Version 2

15-Mar-2023

1. Updated the Handler structure to handle custom handlers for specific data records.
2. Updated the ListRequest to remove the `Expression` dependancies for sorting and filtering, and make the object API compliant.
3. Restructuring the directory and project structure to my latest design.  Splitting out application code into separate libraries.  
