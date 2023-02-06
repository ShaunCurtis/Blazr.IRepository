/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.Core;

public sealed class WeatherForecastEditContext1 : RecordEditContextBase<WeatherForecast>
{
    private DateOnly _date;
    public DateOnly Date
    {
        get => _date;
        set => UpdateifChangedAndNotify(ref _date, value, this.BaseRecord.Date, WeatherForecastConstants.Date);
    }

    private int _temperatureC;
    public int TemperatureC
    {
        get => _temperatureC;
        set => UpdateifChangedAndNotify(ref _temperatureC, value, this.BaseRecord.TemperatureC, WeatherForecastConstants.TemperatureC);
    }

    private string _summary = String.Empty;
    public string Summary
    {
        get => _summary;
        set => UpdateifChangedAndNotify(ref _summary!, value, this.BaseRecord.Summary, WeatherForecastConstants.Summary);
    }

    public WeatherForecastEditContext1(WeatherForecast record) : base(record) { }

    public override void Load(WeatherForecast record, bool notify = true)
    {
        this.BaseRecord = record ??= this.Record with { };
        this.Uid = record.Uid;
        _date = record.Date;
        _temperatureC = record.TemperatureC;
        _summary = record.Summary ?? string.Empty;
    }

    public override WeatherForecast AsNewRecord()
        => this.Record with { Uid = Guid.NewGuid() };

    public override WeatherForecast Record =>
        new WeatherForecast
        {
            Uid = this.Uid,
            Date = _date,
            TemperatureC = _temperatureC,
            Summary = _summary,
        };
}
