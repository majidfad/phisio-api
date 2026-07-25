using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ExerciseCategoryConfiguration : IEntityTypeConfiguration<ExerciseCategory>
{
    public void Configure(EntityTypeBuilder<ExerciseCategory> builder)
    {
        builder.ToTable("exercise_categories");

        builder.HasKey(category => category.ExerciseCategoryId);

        builder.Property(category => category.ExerciseCategoryId)
            .ValueGeneratedNever();

        builder.Property(category => category.NameFa)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(category => category.NameEn)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(category => category.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(category => category.NameFa);
        builder.HasIndex(category => category.NameEn);
        builder.HasIndex(category => category.SortOrder);
    }
}
