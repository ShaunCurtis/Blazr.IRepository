/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.Core;

public record CommandRequest<TRecord>
{
    public required TRecord Item { get; init; }
    public CancellationToken Cancellation { get; set; } = new();
}
