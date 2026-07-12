using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyPatientFeedbacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_patient_feedbacks",
                columns: table => new
                {
                    DailyPatientFeedbackId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ImprovementScore = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_patient_feedbacks", x => x.DailyPatientFeedbackId);
                    table.ForeignKey(
                        name: "FK_daily_patient_feedbacks_asp_net_users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_daily_patient_feedbacks_asp_net_users_PatientId",
                        column: x => x.PatientId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_patient_feedbacks_DoctorId",
                table: "daily_patient_feedbacks",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_daily_patient_feedbacks_FeedbackDate",
                table: "daily_patient_feedbacks",
                column: "FeedbackDate");

            migrationBuilder.CreateIndex(
                name: "ix_daily_patient_feedbacks_patient_id_feedback_date",
                table: "daily_patient_feedbacks",
                columns: new[] { "PatientId", "FeedbackDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_patient_feedbacks");
        }
    }
}
