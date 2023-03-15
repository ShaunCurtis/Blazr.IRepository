/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

using System.ComponentModel.DataAnnotations;

namespace Blazr.App.Core;

public sealed record WeatherForecast : IGuidIdentity
{
    [Key] public Guid Uid { get; init; } = Guid.Empty;

    public DateOnly Date { get; init; } = DateOnly.FromDateTime(DateTime.Now);

    public int TemperatureC { get; init; } = 60;

    public string? Summary { get; init; } = "Testing";
}
