using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorPatients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doctor_patients",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_patients", x => new { x.DoctorId, x.PatientId });
                    table.ForeignKey(
                        name: "FK_doctor_patients_asp_net_users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_doctor_patients_asp_net_users_PatientId",
                        column: x => x.PatientId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_DoctorId",
                table: "doctor_patients",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_DoctorId_PatientId",
                table: "doctor_patients",
                columns: new[] { "DoctorId", "PatientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_PatientId",
                table: "doctor_patients",
                column: "PatientId");

            migrationBuilder.Sql(
                """
                INSERT INTO doctor_patients ("DoctorId", "PatientId", "CreatedAt", "IsEnabled")
                SELECT ue."DoctorId", ue."PatientId", MIN(ue."AssignedAt"), TRUE
                FROM user_exercises ue
                GROUP BY ue."DoctorId", ue."PatientId"
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_patients");
        }
    }
}
