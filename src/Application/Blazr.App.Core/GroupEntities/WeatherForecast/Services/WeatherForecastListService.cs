/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace Blazr.App.Core;

public sealed class WeatherForecastListService
{
    private IDataBroker _dataBroker;

    public IEnumerable<WeatherForecast> Forecasts { get; set; } = Enumerable.Empty<WeatherForecast>();

    public WeatherForecastListService(IDataBroker dataBroker)
        => _dataBroker = dataBroker;

    public async ValueTask GetForecastsAsync(ListQueryRequest request)
    {
        ListQueryResult<WeatherForecast> result = await _dataBroker.GetItemsAsync<WeatherForecast>(request);
        if (result.Successful)
            this.Forecasts = result.Items;
    }

    public async ValueTask<ItemsProviderResult<WeatherForecast>> GetForecastsAsync(ItemsProviderRequest itemsRequest)
    {
        var request = new ListQueryRequest() { StartIndex = itemsRequest.StartIndex, PageSize = itemsRequest.Count };

        ListQueryResult<WeatherForecast> result = await _dataBroker.GetItemsAsync<WeatherForecast>(request);

        return result.Successful
            ? new ItemsProviderResult<WeatherForecast>(result.Items, result.TotalCount > int.MaxValue ? int.MaxValue : (int)result.TotalCount)
            : new ItemsProviderResult<WeatherForecast>(Enumerable.Empty<WeatherForecast>(), 0);
    }
}
