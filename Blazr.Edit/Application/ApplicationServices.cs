using Blazr.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Blazr.Application;

public static class ApplicationServices
{
    public static void AddAppServerDataServices<TDbContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> options) where TDbContext : DbContext
    {
        services.AddDbContextFactory<TDbContext>(options);
        services.AddScoped<IDataBroker, ServerDataBroker<InMemoryWeatherDbContext>>();
        services.AddScoped<IListRequestHandler, ListRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IItemRequestHandler, ItemRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IUpdateRequestHandler, UpdateRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<ICreateRequestHandler, CreateRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<IDeleteRequestHandler, DeleteRequestHandler<InMemoryWeatherDbContext>>();
        services.AddScoped<WeatherForecastListService>();
        services.AddScoped<WeatherForecastEditService>();
    }

    public static void AddTestData(IServiceProvider provider)
    {
        var factory = provider.GetService<IDbContextFactory<InMemoryWeatherDbContext>>();

        if (factory is not null)
            WeatherTestDataProvider.Instance().LoadDbContext<InMemoryWeatherDbContext>(factory);
    }

}
