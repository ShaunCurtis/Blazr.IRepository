/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Linq.Expressions;

namespace Blazr.Core;

public record ReportQueryRequest<TRecord>
{
    public int StartIndex { get; init; } = 0;
    public int PageSize { get; init; } = 1000;
    public CancellationToken CancellationToken { get; set; } = new();
    public Expression<Func<TRecord, bool>>? Filter { get; set; }
    public Expression<Func<TRecord, object>>? Sorter { get; set; }
    public bool SortDescending { get; init; } = false;
}
