using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorPatientStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "doctor_patients",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql(
                """
                UPDATE doctor_patients
                SET "Status" = 2
                WHERE "IsEnabled" = TRUE AND "Status" = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_DoctorId_Status",
                table: "doctor_patients",
                columns: new[] { "DoctorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_PatientId_Status",
                table: "doctor_patients",
                columns: new[] { "PatientId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_doctor_patients_DoctorId_Status",
                table: "doctor_patients");

            migrationBuilder.DropIndex(
                name: "IX_doctor_patients_PatientId_Status",
                table: "doctor_patients");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "doctor_patients");
        }
    }
}
