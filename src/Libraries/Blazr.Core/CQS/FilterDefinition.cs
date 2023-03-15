/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.Core;

public sealed record FilterDefinition
{
    public string FilterName { get; init; } = string.Empty;
    public string FilterData { get; init; } = string.Empty;
}
