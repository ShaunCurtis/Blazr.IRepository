/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Linq.Expressions;

namespace Blazr.Core;

public record ReportRequest : IReportRequest
{
    public string ReportName { get; init; } = string.Empty;
    public int StartIndex { get; init; } = 0;
    public int PageSize { get; init; } = 1000;
    public CancellationToken CancellationToken { get; set; } = new();
    public string? SortField { get; init; }
    public bool SortDescending { get; init; } = false;
}
