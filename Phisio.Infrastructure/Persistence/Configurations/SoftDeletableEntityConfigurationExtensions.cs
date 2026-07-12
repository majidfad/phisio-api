using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Common;

namespace Phisio.Infrastructure.Persistence.Configurations;

internal static class SoftDeletableEntityConfigurationExtensions
{
    public static void ConfigureSoftDelete<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, bool>> isEnabledProperty)
        where TEntity : class
    {
        builder.Property(isEnabledProperty)
            .IsRequired()
            .HasDefaultValue(true);
    }

    public static void ConfigureSoftDelete<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.ConfigureSoftDelete(entity => entity.IsEnabled);
    }
}
