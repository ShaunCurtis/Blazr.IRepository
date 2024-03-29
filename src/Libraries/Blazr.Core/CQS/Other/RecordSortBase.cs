﻿/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

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

    protected static bool TryBuildSortExpression(string sortField, [NotNullWhen(true)] out Expression<Func<TRecord, object>>? expression)
    {
        expression = null;

        Type recordType = typeof(TRecord);
        PropertyInfo sortProperty = recordType.GetProperty(sortField)!;
        if (sortProperty is null)
            return false;

        ParameterExpression parameterExpression = Expression.Parameter(recordType, "item");
        MemberExpression memberExpression = Expression.Property((Expression)parameterExpression, sortField);
        Expression propertyExpression = Expression.Convert(memberExpression, typeof(object));

        expression = Expression.Lambda<Func<TRecord, object>>(propertyExpression, parameterExpression);

        return true;
    }
}

