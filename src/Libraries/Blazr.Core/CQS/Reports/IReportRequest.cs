/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Linq.Expressions;

namespace Blazr.Core;

public interface IReportRequest
{
    public string ReportName { get; }
    public int StartIndex { get; }
    public int PageSize { get; }
    public CancellationToken CancellationToken { get; }
    public string? SortField { get; }
    public bool SortDescending { get; }
}
