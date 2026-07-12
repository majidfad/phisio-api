using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoreModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "exercises",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'UTC'");

            migrationBuilder.AddColumn<string>(
                name: "DifficultyLevel",
                table: "exercises",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Beginner");

            migrationBuilder.CreateTable(
                name: "doctor_patients",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_doctor_patients_PatientId",
                table: "doctor_patients",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_patients");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "DifficultyLevel",
                table: "exercises");
        }
    }
}
