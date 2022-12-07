using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

namespace Blazr.Test
{
    public class DataPipelineTests
    {

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

        [Fact]
        public async Task GetItemsTest()
        {
            var services = new ServiceCollection();
            services.AddAppServerDataServices();
            var rootProvider = services.BuildServiceProvider();

            var providerScope = rootProvider.CreateScope();
            var provider = providerScope.ServiceProvider;

            ApplicationServices.AddTestData(provider);

            var databroker = provider.GetRequiredService<IDataBroker>();

            var testProvider = WeatherTestDataProvider.Instance();

            var request = new ListQueryRequest<WeatherForecast>();
            var result = await databroker.GetItemsAsync<WeatherForecast>(request);

            Assert.NotNull(result);
            Assert.Equal(testProvider.WeatherForecasts.Count(), result.TotalCount);

            providerScope.Dispose();
            rootProvider.Dispose();
        }
    }
}

