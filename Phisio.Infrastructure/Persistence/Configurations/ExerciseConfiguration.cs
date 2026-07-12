using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises");

        builder.HasKey(e => e.ExerciseId);

        builder.Property(e => e.ExerciseId)
            .ValueGeneratedNever();

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.VideoUrl)
            .HasMaxLength(500);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(e => e.Title);

        builder.HasMany(e => e.UserExercises)
            .WithOne(ue => ue.Exercise)
            .HasForeignKey(ue => ue.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
