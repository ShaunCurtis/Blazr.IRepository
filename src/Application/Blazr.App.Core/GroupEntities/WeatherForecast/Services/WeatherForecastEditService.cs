/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.App.Core;

public sealed class WeatherForecastEditService
{
    private IDataBroker _dataBroker;

    public CommandResult LastResult { get; private set; } = CommandResult.Success();

    public readonly WeatherForecastEditContext EditStateContext = new WeatherForecastEditContext(new());

    public WeatherForecastEditService(IDataBroker dataBroker)
        => _dataBroker = dataBroker;

    public async ValueTask GetForecastAsync(Guid id)
        => await GetForecastAsync(new ItemQueryRequest { Uid = id });

    public async ValueTask GetForecastAsync(ItemQueryRequest request)
    {
        ItemQueryResult<WeatherForecast> result = await _dataBroker.GetItemAsync<WeatherForecast>(request);

        if (result.Successful && result.Item is not null)
            EditStateContext.Load(result.Item, true);
    }

    public async ValueTask UpdateForecastAsync()
    {
        if (!this.EditStateContext.IsDirty)
            return;

        var request = new CommandRequest<WeatherForecast> { Item = this.EditStateContext.Record };

        CommandResult result = await _dataBroker.UpdateItemAsync<WeatherForecast>(request);

        if (result.Successful)
            EditStateContext.SetAsSaved();

        this.LastResult = result;
    }
}