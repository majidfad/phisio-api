using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Common;

namespace Phisio.Infrastructure.Persistence.Configurations;

internal static class AuditableEntityConfigurationExtensions
{
    public static void ConfigureCreatedAt<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, DateTime>> createdAtProperty)
        where TEntity : class
    {
        builder.Property(createdAtProperty)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'");
    }

    public static void ConfigureCreatedAt<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : BaseEntity
    {
        builder.ConfigureCreatedAt(entity => entity.CreatedAt);
    }
}
