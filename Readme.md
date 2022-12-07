# Rethinking the Repository Pattern


## Introduction

This is not an regurgitation of how to build the classic `IRepository` in DotNetCore.  The version presented here is a very different animal.

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

4. It implements some good CQS practices and patterns.

## Nomenclature and Terminology

 - **DI**: Dependancy Injection 
 - **CQS**: Command/Query Separation 

## Repo

The Repo and latest version of this article is here: [Blazr.IREpository](https://github.com/ShaunCurtis/Blazr.IRepository).

## The Data Store

The solution needs a real data store for testing: it implements an Entity Framework In-Memory database.

As I'm a Blazor developer, my data class is the good old `WeatherForecast`. The code for and details of the data store are in the Appendix.

Here's the `DbContext` used by the DBContext factory.

```csharp
public class InMemoryWeatherDbContext : DbContext
{
    public DbSet<WeatherForecast> WeatherForecast { get; set; } = default!;
    public InMemoryWeatherDbContext(DbContextOptions<InMemoryWeatherDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<WeatherForecast>().ToTable("WeatherForecast");
}
```

### Testing the Factory and Context

The following test demonstrates the basic datastore setup in DI. It:
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

## The Classic Implementation

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
Let's take a look at it:

1. What happens when a `null` is returned, what does it mean?
2. Did that add/update/delete really succeed?  How do I know?
3. How do you handle cancellation tokens?  Most of the async methods now accept a cancellation token.
4. What happens when your DBset contains a million records (maybe because the DBA got something wrong last night)?
5. .....

## The new Implementation

Another data pipeline implementation is CQS.  It's more verbose than the Repository pattern, but it implements some key good practices that we should use.

### Requests and Results

We need some request and result objects to encapulate what we are requesting and the results ans status information we expect back.  Everything is a record: defined once and then consumed.

We can define a command like this:

#### Commands

*Commands* are requests to make a change to data store: the standard Create/Update/Delete operations.

```csharp
public record CommandRequest<TRecord>
{
    public required TRecord Item { get; init; }
    public CancellationToken Cancellation { get; set; } = new ();
}
```
Handlers for commands only return status information: no data.  We can define the result like this:

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

#### Item Requests

*Queries* are requests to get data from the data store: no mutation.  We can define an item query like this:

```csharp
public record ItemQueryRequest
{
    public required Guid Uid { get; init; }
    public CancellationToken Cancellation { get; set; } = new();
}
```

And the return by the handler: the requested data and status.

```csharp
public record ItemQueryResult<TRecord>
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

#### List Queries

List queries present a few extra challenges.

1. They should never request everything.  In edge conditions there may be 1,000,000+ rows in a table.  Every request should be constrained.  The request defines `StartIndex` and `PageSize` to both constrain the data and provide functionality for paging operations.  If you set the page size to 1,000,000 will your data pipeline and front end handle it gracefully?
2. They need to handle sorting and filtering.  The request defines these as Linq Expressions.

```csharp
public record ListQueryRequest<TRecord>
{
    public int StartIndex { get; init; } = 0;
    public int PageSize { get; init; } = 1000;
    public CancellationToken Cancellation { get; set; } = new ();
    public bool SortDescending { get; } = false;
    public Expression<Func<TRecord, bool>>? FilterExpression { get; init; }
    public Expression<Func<TRecord, object>>? SortExpression { get; init; }
}
```

The result returns the items, and the count of total items in the table/view.  `Items` are always returned as `IEnumerable`.

```csharp
public record ListQueryResult<TRecord>
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

### Handlers

Handlers are small single purpose classes that handle requests.  They abstract the nitty-gritty execution from the higher level Data Broker.

#### Command Handlers

The interface provides the abstraction.

```csharp
public interface ICreateRequestHandler
{
    public ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new();
}
```

And the implementation does the real work.

1. Injects the DBContext Factory.
2. Implements *Unit of Work* Db contexts through the DbContext factory.
3. Uses the Add method on the context to add the record to EF.
4. Calls `SaveChangesAsync`, passing in the Cancellation token, and expects a single change to be reported.
5. Provides status information if things go wrong.

```csharp
public class CreateRequestHandler<TDbContext>
    : ICreateRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public CreateRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

    public async ValueTask<CommandResult> ExecuteAsync<TRecord>(CommandRequest<TRecord> request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No CommandRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();

        dbContext.Add<TRecord>(request.Item);
        return await dbContext.SaveChangesAsync(request.Cancellation) == 1
            ? CommandResult.Success("Record Updated")
            : CommandResult.Failure("Error updating Record");
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
```

And the server implementation. Note:

1. Injects the DBContext Factory.
2. Implements *Unit of Work* Db contexts through the DbContext factory.
3. Turns off tracking.  There's no mutation involved in this transaction.
4. Checks to see if it can use an Id to get the item - the record implements `IGuidIdentity`.
5. If not, tries `FindAsync` which uses the inbuilt `Key` methodology to get the record.
5. Provides status information if things go wrong.


```csharp
public class ItemRequestHandler<TDbContext>
    : IItemRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public ItemRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

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

        if (record is null)
            return ItemQueryResult<TRecord>.Failure("No record retrieved");

        return ItemQueryResult<TRecord>.Success(record);
    }
}
```
#### List Request Handler

The interface.

```csharp
public interface IListRequestHandler
{
    public ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new();
}
```
And implementation.

Note there are two internal methods:

1. `_getItemsAsync` Gets the items.  This builds an `IQueryable` object and returns it as an un-materialized `IEnumerable`.  It only gets executed when enumerated.
2. `_getCountAsync` Gets the count of all the records based on the filter.

```csharp
public class ListRequestHandler<TDbContext> : IListRequestHandler
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _factory;

    public ListRequestHandler(IDbContextFactory<TDbContext> factory)
        => _factory = factory;

    public async ValueTask<ListQueryResult<TRecord>> ExecuteAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new()
    {
        var list = await _getItemsAsync<TRecord>(request);
        var totalCount = await _getCountAsync<TRecord>(request);

        return ListQueryResult<TRecord>.Success(list, totalCount);
    }

    private ValueTask<IEnumerable<TRecord>> _getItemsAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new()
    {
        if (request == null)
            throw new DataPipelineException($"No ListQueryRequest defined in {this.GetType().FullName}");

        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();
        if (request.FilterExpression is not null)
            query = query
                .Where(request.FilterExpression)
                .AsQueryable();

        if (request.SortExpression is not null)

            query = request.SortDescending
                ? query.OrderByDescending(request.SortExpression)
                : query.OrderBy(request.SortExpression);

        if (request.PageSize > 0)
            query = query
                .Skip(request.StartIndex)
                .Take(request.PageSize);

        return ValueTask.FromResult(query.AsEnumerable());
    }

    private async ValueTask<long> _getCountAsync<TRecord>(ListQueryRequest<TRecord> request)
        where TRecord : class, new()
    {
        using var dbContext = _factory.CreateDbContext();
        dbContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<TRecord> query = dbContext.Set<TRecord>();

        if (request.FilterExpression is not null)
            query = query
                .Where(request.FilterExpression)
                .AsQueryable();

        return query is IAsyncEnumerable<TRecord>
            ? await query.CountAsync(request.Cancellation)
            : query.Count();
    }
}
```
### The Repository Class Replacement

First the interface.

The very important bit is the generic `TRecord` definition on each method, not on the interface.  This removes the need for entity specific implementations.

```csharp
public interface IDataBroker
{
    public ValueTask<ListQueryResult<TRecord>> GetItemsAsync<TRecord>(ListQueryRequest<TRecord> request) where TRecord : class, new();
    public ValueTask<ItemQueryResult<TRecord>> GetItemAsync<TRecord>(ItemQueryRequest request) where TRecord : class, new();
    public ValueTask<CommandResult> UpdateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
    public ValueTask<CommandResult> CreateItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
    public ValueTask<CommandResult> DeleteItemAsync<TRecord>(CommandRequest<TRecord> request) where TRecord : class, new();
}
```

And the implementation.  Each handler is registered in DI and injected into the broker.

```csharp
public class RepositoryDataBroker : IDataBroker
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

    public ValueTask<ListQueryResult<TRecord>> GetItemsAsync<TRecord>(ListQueryRequest<TRecord> request) where TRecord : class, new()
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

First two methods to create our root DI container and populate the database.

```csharp
private ServiceProvider BuildRootContainer()
{
    var services = new ServiceCollection();

    // Define the DbSet and Server Type for the DbContext Factory
    services.AddDbContextFactory<InMemoryWeatherDbContext>(options
        => options.UseInMemoryDatabase($"WeatherDatabase-{Guid.NewGuid().ToString()}"));
    // Define the Broker and Handlers
    services.AddScoped<IDataBroker, RepositoryDataBroker>();
    services.AddScoped<IListRequestHandler, ListRequestHandler<InMemoryWeatherDbContext>>();
    services.AddScoped<IItemRequestHandler, ItemRequestHandler<InMemoryWeatherDbContext>>();
    services.AddScoped<IUpdateRequestHandler, UpdateRequestHandler<InMemoryWeatherDbContext>>();
    services.AddScoped<ICreateRequestHandler, CreateRequestHandler<InMemoryWeatherDbContext>>();
    services.AddScoped<IDeleteRequestHandler, DeleteRequestHandler<InMemoryWeatherDbContext>>();

    // Create the container
    return services.BuildServiceProvider();
}

private IDbContextFactory<InMemoryWeatherDbContext> GetPopulatedFactory(IServiceProvider provider)
{
    // get the DbContext factory and add the test data
    var factory = provider.GetService<IDbContextFactory<InMemoryWeatherDbContext>>();
    if (factory is not null)
        WeatherTestDataProvider.Instance().LoadDbContext<InMemoryWeatherDbContext>(factory);

    return factory!;
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
    var rootProvider = this.BuildRootContainer();

    //define a scoped container
    var providerScope = rootProvider.CreateScope();
    var provider = providerScope.ServiceProvider;

    // get the DbContext factory and add the test data
    var factory = this.GetPopulatedFactory(provider);

    // Check we can retrieve thw first 1000 records
    var dbContext = factory!.CreateDbContext();
    Assert.NotNull(dbContext);

    var databroker = provider.GetRequiredService<IDataBroker>();

    var request = new ListQueryRequest<WeatherForecast>();
    var result = await databroker.GetItemsAsync<WeatherForecast>(request);

    Assert.NotNull(result);
    Assert.Equal(testProvider.WeatherForecasts.Count(), result.TotalCount);

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
    var rootProvider = this.BuildRootContainer();

    //define a scoped container
    var providerScope = rootProvider.CreateScope();
    var provider = providerScope.ServiceProvider;

    // get the DbContext factory and add the test data
    var factory = this.GetPopulatedFactory(provider);

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

    // Get the new record
    {
        var request = new ItemQueryRequest() { Uid = newRecord.Uid };
        var result = await databroker.GetItemAsync<WeatherForecast>(request);

        Assert.Equal(newRecord, result.Item);
    }

    // Check the record count has incremented
    {
        var request = new ListQueryRequest<WeatherForecast>();
        var result = await databroker.GetItemsAsync<WeatherForecast>(request);

        Assert.NotNull(result);
        Assert.Equal(testProvider.WeatherForecasts.Count() + 1, result.TotalCount);
    }

    providerScope.Dispose();
    rootProvider.Dispose();
}
```

Wrapping Up

What I've presented here is a Repository Pattern hybrid.  It maintains the simplicity of the Repository Pattern, but applies some of the best bits of the CQS Pattern.  Abstracting the actual nitty-gritty EF and Linq code to individual handlers keeps the classes small, succinct and single purpose.

The single Data Broker simplifies data access for the Core and Presentation domain code.

## Appendix

### The Data Store

The solution needs a real data store to test and run the solution properly.  It implements an Entity Framework In-Memory database.

As I'm a Blazor developer my data class is the good old `WeatherForecast`.  Here's my data class.  Note it is a record for immutability and I've set some arbitary default values for testing purposes.

```csharp
public record WeatherForecast : IGuidIdentity
{
    [Key] public Guid Uid { get; init; } = Guid.Empty;
    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.Now);
    public int TemperatureC { get; init; } = 60;
    public string? Summary { get; init; } = "Testing";
}
```

First a class to generate a data set is both loaded into the database and available directly for running unit tests.  This is a *Singleton* pattern class, not a DI singleton.  There are methods such as `GetRandomRecord` for testing.

```csharp
public class WeatherTestDataProvider
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
public class InMemoryWeatherDbContext
    : DbContext
{
    public DbSet<WeatherForecast> WeatherForecast { get; set; } = default!;
    public InMemoryWeatherDbContext(DbContextOptions<InMemoryWeatherDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<WeatherForecast>().ToTable("WeatherForecast");
}
```
