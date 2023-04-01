/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.App.Infrastructure;

// This is not the way to implement a Presenter
public sealed class WeatherForecastEditPresenter
{
    private readonly IDbContextFactory<InMemoryWeatherDbContext> _factory;

    public WeatherForecastEditPresenter(IDbContextFactory<InMemoryWeatherDbContext> factory)
        => _factory = factory;


    public async ValueTask UpdateForecastAsync(WeatherForecast weatherForecast)
    {
        using var dbContext = _factory.CreateDbContext();
        dbContext.Add<WeatherForecast>(weatherForecast);
        var recordsChanged = await dbContext.SaveChangesAsync();

        if (recordsChanged != 1)
        {
            // Do somthing
        }
    }
}
