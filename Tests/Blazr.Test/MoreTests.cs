/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Test;

public class MoreTests
{

    [Fact]
    public async Task DBContextTest()
    {
        // Get our test provider to use as our control
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

    [Fact]
    public async Task GetItemsTest()
    {
        // Get our test provider to use as our control
        var testProvider = WeatherTestDataProvider.Instance();

        // Build the root DI Container
        var rootProvider = ServiceContainers.BuildRootContainer();

        //define a scoped container
        var providerScope = rootProvider.CreateScope();
        IServiceProvider provider = providerScope.ServiceProvider;

        // get the DbContext factory and add the test data
        var factory = ServiceContainers.GetPopulatedFactory(provider);

        // Check we can retrieve thw first 1000 records
        var dbContext = factory!.CreateDbContext();
        Assert.NotNull(dbContext);

        var dbb = provider.GetRequiredService<IDataBroker>();
        var dataBroker = ActivatorUtilities.GetServiceOrCreateInstance<IDataBroker>(rootProvider);
        var request = new ListQueryRequest();
        var result = await dataBroker.GetItemsAsync<WeatherForecast>(request);

        Assert.NotNull(result);
        Assert.Equal(testProvider.WeatherForecasts.Count(), result.TotalCount);

        providerScope.Dispose();
        rootProvider.Dispose();
    }

}
