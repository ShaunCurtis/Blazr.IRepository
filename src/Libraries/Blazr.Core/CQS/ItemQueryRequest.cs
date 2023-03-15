/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.Core;

public sealed record ItemQueryRequest
{
    public required Guid Uid { get; init; }
    public CancellationToken Cancellation { get; set; } = new();

}
