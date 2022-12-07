/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Core;

public interface IRecordEditContext<TRecord> : IEditContext
    where TRecord : class, new()
{
    public Guid Uid { get; }

    public TRecord BaseRecord { get; }

    public TRecord Record { get; }

    public void Load(TRecord record, bool notify = true);

    public TRecord AsNewRecord();

    public void Reset();

    public void SetAsSaved();
}