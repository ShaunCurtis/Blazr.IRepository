/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Test;

public class DataPipelineTests
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

    [Fact]
    public async Task GetSortedItemsTest()
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

        var request = new ListQueryRequest() { SortDescending=false, SortField = WeatherForecastConstants.Summary };

        var result = await databroker.GetItemsAsync<WeatherForecast>(request);

        var expectedCount = testProvider.WeatherForecasts.Count();

        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.TotalCount);
        Assert.Equal("Balmy" , result.Items.First().Summary);
        Assert.Equal("Balmy", result.Items.Skip(1).First().Summary);
        Assert.Equal("Balmy", result.Items.Skip(2).First().Summary);

        providerScope.Dispose();
        rootProvider.Dispose();
    }


    [Theory]
    [InlineData(10) ]
    [InlineData(20)]
    public async Task GetSortedAndPagedItemsTest(int pageSize)
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

        var request = new ListQueryRequest() { SortDescending = false, SortField = WeatherForecastConstants.Summary, StartIndex = 0, PageSize=pageSize };

        var result = await databroker.GetItemsAsync<WeatherForecast>(request);

        var expectedCount = testProvider.WeatherForecasts.Count();

        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.TotalCount);
        Assert.Equal("Balmy", result.Items.First().Summary);
        Assert.Equal("Balmy", result.Items.Skip(1).First().Summary);
        Assert.Equal("Balmy", result.Items.Skip(2).First().Summary);
        Assert.Equal(pageSize , result.Items.Count());

        providerScope.Dispose();
        rootProvider.Dispose();
    }

    [Fact]
    public async Task GetFilteredItemsTest()
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

        var filters = new List<FilterDefinition> { new(WeatherForecastConstants.BySummary, "Balmy") };
        var request = new ListQueryRequest() { SortDescending = false, Filters = filters };

        var result = await databroker.GetItemsAsync<WeatherForecast>(request);

        var expectedCount = testProvider.WeatherForecasts.Where(item => item.Summary == "Balmy").Count();

        Assert.NotNull(result);
        Assert.Equal(expectedCount, result.TotalCount);

        providerScope.Dispose();
        rootProvider.Dispose();
    }

    [Fact]
    public async Task GetItemTest()
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

        // Test an arbitary record
        var testRecord = testProvider.GetRandomRecord()!;

        var request = new ItemQueryRequest() { Uid = testRecord.Uid };
        var result = await databroker.GetItemAsync<WeatherForecast>(request);

        Assert.NotNull(result);
        Assert.True(result.Successful);
        Assert.Equal(testRecord, result.Item);

        providerScope.Dispose();
        rootProvider.Dispose();
    }

    [Fact]
    public async Task UpdateItemTest()
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

        // Test an arbitary record
        var testRecord = testProvider.GetRandomRecord()!;
        var updatedRecord = testRecord with { Summary = "Update Testing" };

        // Update the Record
        {
            var request = new CommandRequest<WeatherForecast>() { Item = updatedRecord };
            var result = await databroker.UpdateItemAsync<WeatherForecast>(request);

            Assert.NotNull(result);
            Assert.True(result.Successful);
        }

        // Get the record and check it's been updated
        {
            var request = new ItemQueryRequest() { Uid = testRecord.Uid };
            var result = await databroker.GetItemAsync<WeatherForecast>(request);

            Assert.Equal(updatedRecord, result.Item);
            Assert.True(result.Successful);
        }

        providerScope.Dispose();
        rootProvider.Dispose();
    }

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

        // Get the new record
        {
            var request = new ItemQueryRequest() { Uid = newRecord.Uid };
            var result = await databroker.GetItemAsync<WeatherForecast>(request);

            Assert.Equal(newRecord, result.Item);
        }

        // Check the record count has incremented
        {
            var request = new ListQueryRequest();
            var result = await databroker.GetItemsAsync<WeatherForecast>(request);

            Assert.NotNull(result);
            Assert.Equal(testProvider.WeatherForecasts.Count() + 1, result.TotalCount);
        }

        providerScope.Dispose();
        rootProvider.Dispose();
    }

    [Fact]
    public async Task DeleteItemTest()
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

        // Test an arbitary record
        var testRecord = testProvider.GetRandomRecord()!;
        var updatedRecord = testRecord with { Summary = "Update Testing" };

        // Delete the Record
        {
            var request = new CommandRequest<WeatherForecast>() { Item = updatedRecord };
            var result = await databroker.DeleteItemAsync<WeatherForecast>(request);

            Assert.NotNull(result);
            Assert.True(result.Successful);
        }

        // Try to Get the record and check it's not there
        {
            var request = new ItemQueryRequest() { Uid = testRecord.Uid };
            var result = await databroker.GetItemAsync<WeatherForecast>(request);

            Assert.False(result.Successful);
        }


        // Check the record count has decremented
        {
            var request = new ListQueryRequest();
            var result = await databroker.GetItemsAsync<WeatherForecast>(request);

            Assert.NotNull(result);
            Assert.Equal(testProvider.WeatherForecasts.Count() - 1, result.TotalCount);
        }

        providerScope.Dispose();
        rootProvider.Dispose();
    }
}

