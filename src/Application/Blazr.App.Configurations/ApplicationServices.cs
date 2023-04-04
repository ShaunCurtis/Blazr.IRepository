/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.App.Configurations;

public static class ApplicationServices
{
    public static void AddAppServerDataServices<TDbContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TDbContext : DbContext
    {
        services.AddDbContextFactory<TDbContext>(options);
        services.AddScoped<IDataBroker, ServerDataBroker>();
        services.AddScoped<IListRequestHandler, ListRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IItemRequestHandler, ItemRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IUpdateRequestHandler, UpdateRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<ICreateRequestHandler, CreateRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IDeleteRequestHandler, DeleteRequestServerHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<WeatherForecastListService>();
        services.AddScoped<WeatherForecastEditService>();
    }

    public static void AddAppServerDataServices(this IServiceCollection services)
    {
        services.AddAppServerDataServices<InMemoryWeatherDbContext>(options
            => options.UseInMemoryDatabase($"WeatherDatabase-{Guid.NewGuid().ToString()}"));
    }

    public static void AddAppTestDataServices(this IServiceCollection services)
    {
        services.AddDbContextFactory<InMemoryWeatherDbContext>(options
            => options.UseInMemoryDatabase($"WeatherDatabase-{Guid.NewGuid().ToString()}"));
    }

    public static void AddTestData(IServiceProvider provider)
    {
        var factory = provider.GetService<IDbContextFactory<InMemoryWeatherDbContext>>();

        if (factory is not null)
            WeatherTestDataProvider.Instance().LoadDbContext<InMemoryWeatherDbContext>(factory);
    }

}
