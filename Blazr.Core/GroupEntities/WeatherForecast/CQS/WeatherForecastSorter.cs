/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Linq.Expressions;

namespace Blazr.App.Core;

public class WeatherForecastSorter : RecordSortBase<WeatherForecast>, IRecordSorter<WeatherForecast>
{
    public  IQueryable<WeatherForecast> AddSortToQuery(string fieldName, IQueryable<WeatherForecast> query, bool sortDescending)
        => fieldName switch
        {
            WeatherForecastConstants.TemperatureC => Sort(query, sortDescending, OnTemperature),
            WeatherForecastConstants.Summary => Sort(query, sortDescending, OnSummary),
            _ => Sort(query, sortDescending, OnDate)
        };

    private static Expression<Func<WeatherForecast, object>> OnDate => item => item.Date;
    private static Expression<Func<WeatherForecast, object>> OnTemperature => item => item.TemperatureC;
    private static Expression<Func<WeatherForecast, object>> OnSummary => item => item.Summary ?? string.Empty;
}
