using Microsoft.EntityFrameworkCore;
using Phisio.Domain.Common;

namespace Phisio.Infrastructure.Persistence;

internal static class SoftDeleteFilterConfiguration
{
    public static void ApplySoftDeleteQueryFilters(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(SoftDeleteFilterConfiguration)
                .GetMethod(
                    nameof(ConfigureFilter),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(null, [modelBuilder]);
        }
    }

    private static void ConfigureFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => entity.IsEnabled);
    }
}
