using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicIdToDoctorPatients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_doctor_patients",
                table: "doctor_patients");

            migrationBuilder.DropIndex(
                name: "IX_doctor_patients_DoctorId_PatientId",
                table: "doctor_patients");

            migrationBuilder.AddColumn<Guid>(
                name: "ClinicId",
                table: "doctor_patients",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE doctor_patients dp
                SET "ClinicId" = (
                    SELECT cd."ClinicId"
                    FROM clinic_doctors cd
                    WHERE cd."DoctorId" = dp."DoctorId"
                    ORDER BY cd."ClinicId"
                    LIMIT 1
                );
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM doctor_patients WHERE "ClinicId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot migrate doctor_patients: one or more doctors have no clinic membership.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ClinicId",
                table: "doctor_patients",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_doctor_patients",
                table: "doctor_patients",
                columns: new[] { "DoctorId", "PatientId", "ClinicId" });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_ClinicId",
                table: "doctor_patients",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_PatientId_DoctorId_ClinicId",
                table: "doctor_patients",
                columns: new[] { "PatientId", "DoctorId", "ClinicId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_doctor_patients_clinics_ClinicId",
                table: "doctor_patients",
                column: "ClinicId",
                principalTable: "clinics",
                principalColumn: "ClinicId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_doctor_patients_clinics_ClinicId",
                table: "doctor_patients");

            migrationBuilder.DropPrimaryKey(
                name: "PK_doctor_patients",
                table: "doctor_patients");

            migrationBuilder.DropIndex(
                name: "IX_doctor_patients_ClinicId",
                table: "doctor_patients");

            migrationBuilder.DropIndex(
                name: "IX_doctor_patients_PatientId_DoctorId_ClinicId",
                table: "doctor_patients");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "doctor_patients");

            migrationBuilder.AddPrimaryKey(
                name: "PK_doctor_patients",
                table: "doctor_patients",
                columns: new[] { "DoctorId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_patients_DoctorId_PatientId",
                table: "doctor_patients",
                columns: new[] { "DoctorId", "PatientId" },
                unique: true);
        }
    }
}
