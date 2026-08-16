using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clinics",
                columns: table => new
                {
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClinicManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinics", x => x.ClinicId);
                    table.ForeignKey(
                        name: "FK_clinics_asp_net_users_ClinicManagerId",
                        column: x => x.ClinicManagerId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clinic_doctors",
                columns: table => new
                {
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_doctors", x => new { x.ClinicId, x.DoctorId });
                    table.ForeignKey(
                        name: "FK_clinic_doctors_asp_net_users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clinic_doctors_clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinics",
                        principalColumn: "ClinicId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clinic_phone_numbers",
                columns: table => new
                {
                    ClinicPhoneNumberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinic_phone_numbers", x => x.ClinicPhoneNumberId);
                    table.ForeignKey(
                        name: "FK_clinic_phone_numbers_clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "clinics",
                        principalColumn: "ClinicId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clinic_doctors_ClinicId",
                table: "clinic_doctors",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_clinic_doctors_ClinicId_DoctorId",
                table: "clinic_doctors",
                columns: new[] { "ClinicId", "DoctorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clinic_doctors_DoctorId",
                table: "clinic_doctors",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_clinic_phone_numbers_ClinicId",
                table: "clinic_phone_numbers",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_clinics_ClinicManagerId",
                table: "clinics",
                column: "ClinicManagerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clinic_doctors");

            migrationBuilder.DropTable(
                name: "clinic_phone_numbers");

            migrationBuilder.DropTable(
                name: "clinics");
        }
    }
}
