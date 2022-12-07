/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================

namespace Blazr.Core;

/// <summary>
/// Base abstact class for Record edit context implementations that implement
/// most of the boilerplate code
/// </summary>
/// <typeparam name="TRecord"></typeparam>

public abstract class RecordEditContextBase<TRecord> : IRecordEditContext<TRecord>, IEditContext
    where TRecord : class, new()
{
    public TRecord BaseRecord { get; protected set; } = new();
    public virtual Guid Uid { get; protected set; }
    public virtual TRecord Record => new();
    public virtual bool IsDirty => !BaseRecord.Equals(Record);
    public bool IsNew => Uid == Guid.Empty;
    
    public event EventHandler<string?>? FieldChanged;
    public event EventHandler<bool>? EditStateContextUpdated;

    public RecordEditContextBase(TRecord record)
        => Load(record);

    public abstract void Load(TRecord record, bool notify = true);

    public abstract TRecord AsNewRecord();

    public void Reset()
        => Load(BaseRecord);

    public void SetAsSaved()
       => Load(Record);

    protected void NotifyFieldChanged(string? fieldName)
    {
        FieldChanged?.Invoke(null, fieldName);
        EditStateContextUpdated?.Invoke(null, IsDirty);
    }

    protected bool UpdateifChangedAndNotify<TType>(ref TType currentValue, TType value, TType originalValue, string fieldName)
    {
        var hasChanged = !value?.Equals(currentValue) ?? currentValue is not null;
        // Not used here, but we will
        var hasChangedFromOriginal = !value?.Equals(originalValue) ?? originalValue is not null;

        if (hasChanged)
        {
            currentValue = value;
            NotifyFieldChanged(fieldName);
        }

        return hasChanged;
    }
}
