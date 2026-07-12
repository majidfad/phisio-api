using Microsoft.EntityFrameworkCore;
using Phisio.Domain.Common;

namespace Phisio.Infrastructure.Persistence;

public static class SoftDeleteExtensions
{
    public static void SoftDelete(this ISoftDeletable entity)
    {
        entity.IsEnabled = false;
    }

    public static void SoftDeleteRange(IEnumerable<ISoftDeletable> entities)
    {
        foreach (var entity in entities)
        {
            entity.SoftDelete();
        }
    }

    /// <summary>
    /// Bypasses global soft-delete filters. Use for admin recycle-bin style queries.
    /// </summary>
    public static IQueryable<T> IncludingDisabled<T>(this IQueryable<T> query)
        where T : class => query.IgnoreQueryFilters();

    public static IQueryable<T> ApplyIncludeDisabled<T>(this IQueryable<T> query, bool includeDisabled)
        where T : class => includeDisabled ? query.IncludingDisabled() : query;

    /// <summary>
    /// Filters by enabled status. When <paramref name="isEnabled"/> is true, the global
    /// soft-delete filter applies. When false, only disabled records are returned.
    /// </summary>
    public static IQueryable<T> WhereEnabledStatus<T>(this IQueryable<T> query, bool isEnabled)
        where T : class, ISoftDeletable =>
        isEnabled
            ? query
            : query.IgnoreQueryFilters().Where(entity => !entity.IsEnabled);
}
