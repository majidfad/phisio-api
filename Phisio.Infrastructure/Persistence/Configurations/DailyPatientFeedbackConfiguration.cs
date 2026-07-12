using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;
using Phisio.Infrastructure.Identity;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class DailyPatientFeedbackConfiguration : IEntityTypeConfiguration<DailyPatientFeedback>
{
    public void Configure(EntityTypeBuilder<DailyPatientFeedback> builder)
    {
        builder.ToTable("daily_patient_feedbacks");

        builder.HasKey(f => f.DailyPatientFeedbackId);

        builder.Property(f => f.DailyPatientFeedbackId)
            .ValueGeneratedNever();

        builder.Property(f => f.PatientId)
            .IsRequired();

        builder.Property(f => f.DoctorId)
            .IsRequired();

        builder.Property(f => f.FeedbackDate)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(f => f.ImprovementScore)
            .IsRequired();

        builder.Property(f => f.Comment)
            .HasMaxLength(1000);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(f => new { f.PatientId, f.FeedbackDate })
            .IsUnique()
            .HasDatabaseName("ix_daily_patient_feedbacks_patient_id_feedback_date");

        builder.HasIndex(f => f.DoctorId);
        builder.HasIndex(f => f.FeedbackDate);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
