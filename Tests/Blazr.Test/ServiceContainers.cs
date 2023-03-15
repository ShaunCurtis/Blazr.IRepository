namespace Blazr.Test;

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
