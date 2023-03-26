# Do I Really Need the IRepository and Unit of Work Patterns

The IRepository is close to being a standard in database access in small to medium projects.  Combined with the *Unit of Work* pattern, it provides an ideal solution to abstract data access and persistance from the core application code.

The most common implementation in DotNetCore aplications is to use the patterns over Entity Framework[EF].  While this was good practice in early versions of EF, EF now implements those patterns.

```csharp
public DbSet<WeatherForecast> WeatherForecast { get; set; } = default!;
```

Is a Repository.  It implements *CRUD* and list operations on the `WeatherForecast` record.

You have:

1. dbContext.Add<TRecord>(record);
1. dbContext.Update<TRecord>(record);
1. dbContext.Remove<TRecord>(record);
1. dbContext.Update<TRecord>(record);
1. dbContext.Find{Async}<TRecord>(record);

And the DbSet itself implements `IQueryable` and thus `IWnumerable` for *get many* operations.

Add the `DbContextFactory` and you have Unit of Work:

```csharp
using var dbContext = _factory.CreateDbContext();
dbContext.Add<TRecord>(record);
var recordsChanged = await dbContext.SaveChangesAsync(cancellationToken);
```
