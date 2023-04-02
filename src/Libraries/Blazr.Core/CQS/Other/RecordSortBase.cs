/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Linq.Expressions;

namespace Blazr.Core;

public class RecordSortBase<TRecord>
    where TRecord : class
{
    protected static IQueryable<TRecord> Sort(IQueryable<TRecord> query, bool sortDescending, Expression<Func<TRecord, object>> sorter)
    {
        return sortDescending
            ? query.OrderByDescending(sorter)
            : query.OrderBy(sorter);
    }
}

