/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.App.Infrastructure;

/// <summary>
/// A class to build a fixed data set for testing
/// </summary>
public sealed class WeatherTestDataProvider
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
