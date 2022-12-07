
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Options;
/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Application;

public static class ApplicationServices
{
    public static void AddAppServerDataServices<TDbContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TDbContext : DbContext
    {
        services.AddDbContextFactory<TDbContext>(options);
        services.AddScoped<IDataBroker, RepositoryDataBroker>();
        services.AddScoped<IListRequestHandler, ListRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IItemRequestHandler, ItemRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IUpdateRequestHandler, UpdateRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<ICreateRequestHandler, CreateRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IDeleteRequestHandler, DeleteRequestHandler<InMemoryWeatherDbContext>>();
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
