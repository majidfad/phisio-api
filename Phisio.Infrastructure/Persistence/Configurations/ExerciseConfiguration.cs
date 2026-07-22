using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Identity;

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

        builder.Property(e => e.Instructions)
            .IsRequired()
            .HasMaxLength(4000)
            .HasDefaultValue(string.Empty);

        builder.Property(e => e.VideoUrl)
            .HasMaxLength(2000);

        builder.Property(e => e.MediaType)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExerciseMediaType.UploadedVideo);

        builder.Property(e => e.BodyRegion)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExerciseBodyRegion.Other);

        builder.Property(e => e.Equipment)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExerciseEquipment.None);

        builder.Property(e => e.Difficulty)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExerciseDifficulty.Moderate);

        builder.Property(e => e.CreatedByDoctorId);

        builder.Property(e => e.IsClinicShared)
            .IsRequired()
            .HasDefaultValue(true);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(e => e.Title);
        builder.HasIndex(e => e.CreatedByDoctorId);
        builder.HasIndex(e => new { e.IsClinicShared, e.IsEnabled });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByDoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(e => e.UserExercises)
            .WithOne(ue => ue.Exercise)
            .HasForeignKey(ue => ue.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
