using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Phisio.Infrastructure.Persistence;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723120000_AddDailyFeedbackHardnessAndDoctorUnique")]
public partial class AddDailyFeedbackHardnessAndDoctorUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_daily_patient_feedbacks_patient_id_feedback_date",
            table: "daily_patient_feedbacks");

        migrationBuilder.AddColumn<int>(
            name: "HardnessScore",
            table: "daily_patient_feedbacks",
            type: "integer",
            nullable: false,
            defaultValue: 3);

        migrationBuilder.CreateIndex(
            name: "ix_daily_patient_feedbacks_patient_doctor_feedback_date",
            table: "daily_patient_feedbacks",
            columns: new[] { "PatientId", "DoctorId", "FeedbackDate" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_daily_patient_feedbacks_patient_doctor_feedback_date",
            table: "daily_patient_feedbacks");

        migrationBuilder.DropColumn(
            name: "HardnessScore",
            table: "daily_patient_feedbacks");

        migrationBuilder.CreateIndex(
            name: "ix_daily_patient_feedbacks_patient_id_feedback_date",
            table: "daily_patient_feedbacks",
            columns: new[] { "PatientId", "FeedbackDate" },
            unique: true);
    }
}
