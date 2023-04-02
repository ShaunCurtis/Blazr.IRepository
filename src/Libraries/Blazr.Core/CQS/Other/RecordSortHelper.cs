/// ============================================================
/// Author: Shaun Curtis, Cold Elm Coders
/// License: Use And Donate
/// If you use it, donate something to a charity somewhere
/// ============================================================
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Blazr.Core;

public static class RecordSortHelper
{
    public static bool TryBuildSortExpression<TRecord>(string sortField, [NotNullWhen(true)] out Expression<Func<TRecord, object>>? expression)
        where TRecord : class
    {
        expression = BuildSortExpression<TRecord>(sortField);
        return expression is not null;
    }

    public static Expression<Func<TRecord, object>>? BuildSortExpression<TRecord>(string? sortField)
        where TRecord : class
    {
        if (sortField is null)
            return null;

        Type recordType = typeof(TRecord);
        PropertyInfo sortProperty = recordType.GetProperty(sortField)!;
        if (sortProperty is null)
            return null;

        ParameterExpression parameterExpression = Expression.Parameter(recordType, "item");
        MemberExpression memberExpression = Expression.Property((Expression)parameterExpression, sortField);
        Expression propertyExpression = Expression.Convert(memberExpression, typeof(object));

        return Expression.Lambda<Func<TRecord, object>>(propertyExpression, parameterExpression);
    }
}

