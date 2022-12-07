# Building a VERY Generic IRepository Data Pipeline

## Introduction

This is not an regurgitation of how you build the classic `IRepository` in DotNetCore.  The version presented here is a very different animal.  How so:

1. There's no implementation per entity class.  You won't see this:

```csharp
    public class WeatherForecastRepository : GenericRepository<WeatherForecast>, IWeatherForcastRepository
    {
        public WeatherForecastRepository(DbContextClass dbContext) : base(dbContext) {}
    }

    public interface IProductRepository : IGenericRepository<WeatherForecast> { }
```

2. There's no separate `UnitOfWork` classes, it's built in.

3. All standard Data I/O goes through a single Data Broker.

4. Some aspects of CQS are implemented.

## The Data Store

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

### Testing the Factory and Context

The following test sets up a DI container, loads the data from the Test Provider, and tests that we have the crrect number of records and one arbitary record from the data set.
```csharp
[Fact]
public async Task DBContextTest()
{
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

    providerScope.Dispose();
    rootProvider.Dispose();
}
```

## The Classic Class

Here's a nice succinct implementation.  What's wrong:

1. What happens when a `null` is returned, what does it mean?
2. Did that add/update/delete succeed?
3. How do you handle cancellation tokens?
4. What happens when you DBset contains a million records (maybe because the DBA got something wrong last night)?

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

Before we re-define the class, we need some request and result objects.

Our base requests looks like this:

```csharp
public record CommandRequest<TRecord>
{
    public required TRecord Item { get; init; }
    public CancellationToken Cancellation { get; set; } = new ();
}
```

```csharp
public record ItemQueryRequest
{
    public required Guid Uid { get; init; }
    public CancellationToken Cancellation { get; set; } = new();
}
```

```csharp
public record ItemQueryRequest
{
    public required Guid Uid { get; init; }
    public CancellationToken Cancellation { get; set; } = new();
}
```

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

And our base Results:

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


```csharp
```
