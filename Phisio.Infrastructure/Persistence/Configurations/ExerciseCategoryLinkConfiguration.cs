using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ExerciseCategoryLinkConfiguration : IEntityTypeConfiguration<ExerciseCategoryLink>
{
    public void Configure(EntityTypeBuilder<ExerciseCategoryLink> builder)
    {
        builder.ToTable("exercise_category_links");

        builder.HasKey(link => new { link.ExerciseId, link.ExerciseCategoryId });

        builder.HasOne(link => link.Exercise)
            .WithMany(exercise => exercise.CategoryLinks)
            .HasForeignKey(link => link.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Category)
            .WithMany(category => category.ExerciseLinks)
            .HasForeignKey(link => link.ExerciseCategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(link => link.ExerciseCategoryId);
    }
}
