/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;

namespace Blazr.App.Core;

public class WeatherForecastFilter : IRecordFilter<WeatherForecast>
{
    public IQueryable<WeatherForecast> AddFilterToQuery(IEnumerable<FilterDefinition> filters, IQueryable<WeatherForecast> query)
    {
        foreach (var filter in filters)
        {
            switch (filter.FilterName)
            {
                case WeatherForecastConstants.ByTemerature:
                    if (BindConverter.TryConvertTo<int>(filter.FilterData, null, out int temperatureValue))
                        query = query.Where(ByTemperature(temperatureValue));
                    break;

                case WeatherForecastConstants.BySummary:
                    if (!string.IsNullOrWhiteSpace(filter.FilterData))
                        query = query.Where(BySummary(filter.FilterData));
                    break;

                case WeatherForecastConstants.TemeratureLessThan:
                    if (BindConverter.TryConvertTo<int>(filter.FilterData, null, out int value))
                        query = query.Where(TemperatureLessThan(value));
                    break;

                default:
                    break;
            }
        }

        if (query is IQueryable)
            return query;

        return query.AsQueryable();
    }

    private static Expression<Func<WeatherForecast, bool>> ByTemperature(int value) => item => item.TemperatureC.Equals(value);
    private static Expression<Func<WeatherForecast, bool>> BySummary(string value) => item => item.Summary == value;
    private static Expression<Func<WeatherForecast, bool>> TemperatureLessThan(int value) => item => item.TemperatureC < value;
}
