/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
namespace Blazr.Core;

public interface IEditContext
{
    public event EventHandler<bool>? EditStateContextUpdated;
    public event EventHandler<string?>? FieldChanged;

    public bool IsDirty { get; }

    public bool IsNew { get; }
}
