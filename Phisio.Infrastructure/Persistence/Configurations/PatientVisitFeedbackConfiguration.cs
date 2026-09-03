using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phisio.Domain.Entities;

namespace Phisio.Infrastructure.Persistence.Configurations;

public class PatientVisitFeedbackConfiguration : IEntityTypeConfiguration<PatientVisitFeedback>
{
    public void Configure(EntityTypeBuilder<PatientVisitFeedback> builder)
    {
        builder.ToTable("patient_visit_feedbacks");

        builder.HasKey(f => f.PatientVisitFeedbackId);

        builder.Property(f => f.PatientVisitFeedbackId)
            .ValueGeneratedNever();

        builder.Property(f => f.PatientVisitId)
            .IsRequired();

        builder.Property(f => f.SatisfactionScore)
            .IsRequired();

        builder.Property(f => f.DoctorCommunicationScore)
            .IsRequired();

        builder.Property(f => f.Comment)
            .HasMaxLength(PatientVisitFeedback.MaxCommentLength);

        builder.ConfigureCreatedAt();
        builder.ConfigureSoftDelete();

        builder.HasIndex(f => f.PatientVisitId)
            .IsUnique()
            .HasDatabaseName("ix_patient_visit_feedbacks_patient_visit_id");

        builder.HasOne(f => f.Visit)
            .WithOne()
            .HasForeignKey<PatientVisitFeedback>(f => f.PatientVisitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
