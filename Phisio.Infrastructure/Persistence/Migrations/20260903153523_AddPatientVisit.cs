using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "patient_visits",
                columns: table => new
                {
                    PatientVisitId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisitAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoctorNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patient_visits", x => x.PatientVisitId);
                    table.ForeignKey(
                        name: "FK_patient_visits_asp_net_users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_patient_visits_asp_net_users_PatientId",
                        column: x => x.PatientId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_patient_visits_clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinics",
                        principalColumn: "ClinicId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_patient_visits_ClinicId",
                table: "patient_visits",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_visits_ClinicId_VisitAt",
                table: "patient_visits",
                columns: new[] { "ClinicId", "VisitAt" });

            migrationBuilder.CreateIndex(
                name: "IX_patient_visits_DoctorId",
                table: "patient_visits",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_visits_DoctorId_VisitAt",
                table: "patient_visits",
                columns: new[] { "DoctorId", "VisitAt" });

            migrationBuilder.CreateIndex(
                name: "IX_patient_visits_PatientId",
                table: "patient_visits",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_patient_visits_PatientId_VisitAt",
                table: "patient_visits",
                columns: new[] { "PatientId", "VisitAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "patient_visits");
        }
    }
}
