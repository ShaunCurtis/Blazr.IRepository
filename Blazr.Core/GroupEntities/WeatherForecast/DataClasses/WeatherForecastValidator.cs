/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.App.Core;

public class WeatherForecastValidator : AbstractValidator<WeatherForecastEditContext>
{
    public WeatherForecastValidator()
    {
        this.RuleFor(p => p.Date)
            .NotEmpty()
            .WithMessage("The date must be in the future.");

        this.RuleFor(p => p.Date)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Now))
            .WithMessage("The date must be in the future.");

        this.RuleFor(p => p.TemperatureC)
            .NotNull()
            .WithMessage("Valid values are between -40 and 80 degrees.");

        this.RuleFor(p => p.TemperatureC)
            .LessThanOrEqualTo(80)
            .WithMessage("Valid values are between -40 and 80 degrees.");

        this.RuleFor(p => p.TemperatureC)
            .GreaterThanOrEqualTo(-40)
            .WithMessage("Valid values are between -40 and 80 degrees.");

        this.RuleFor(p => p.Summary)
            .NotEmpty()
            .WithMessage("You must select a Summary.");
    }
}
