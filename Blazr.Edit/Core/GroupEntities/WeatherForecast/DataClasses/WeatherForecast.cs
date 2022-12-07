using System.ComponentModel.DataAnnotations;

namespace Blazr.Core;

public record WeatherForecast : IGuidIdentity
{
    [Key] public Guid Uid { get; init; } = Guid.Empty;

    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.Now);

    public int TemperatureC { get; init; } = 60;

    public string? Summary { get; init; } = "Testing";
}