using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientVisitFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_visit_feedbacks",
                columns: table => new
                {
                    PatientVisitFeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientVisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    SatisfactionScore = table.Column<int>(type: "integer", nullable: false),
                    DoctorCommunicationScore = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_visit_feedbacks", x => x.PatientVisitFeedbackId);
                    table.ForeignKey(
                        name: "FK_patient_visit_feedbacks_patient_visits_PatientVisitId",
                        column: x => x.PatientVisitId,
                        principalTable: "patient_visits",
                        principalColumn: "PatientVisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_patient_visit_feedbacks_patient_visit_id",
                table: "patient_visit_feedbacks",
                column: "PatientVisitId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_visit_feedbacks");
        }
    }
}
