using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doctor_profiles",
                columns: table => new
                {
                    DoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Specialty = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MedicalLicenseNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ClinicAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_profiles", x => x.DoctorProfileId);
                    table.ForeignKey(
                        name: "FK_doctor_profiles_asp_net_users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_doctor_profiles_DoctorId",
                table: "doctor_profiles",
                column: "DoctorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_doctor_profiles_MedicalLicenseNumber",
                table: "doctor_profiles",
                column: "MedicalLicenseNumber",
                unique: true,
                filter: "\"MedicalLicenseNumber\" <> ''");

            migrationBuilder.Sql(
                """
                INSERT INTO doctor_profiles ("DoctorProfileId", "DoctorId", "Specialty", "MedicalLicenseNumber", "ClinicAddress", "CreatedAt", "IsEnabled")
                SELECT gen_random_uuid(), u."Id", '', '', '', COALESCE(u."CreatedAt", NOW() AT TIME ZONE 'UTC'), TRUE
                FROM asp_net_users u
                WHERE u."Role" = 'Doctor'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM doctor_profiles dp
                      WHERE dp."DoctorId" = u."Id"
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doctor_profiles");
        }
    }
}
