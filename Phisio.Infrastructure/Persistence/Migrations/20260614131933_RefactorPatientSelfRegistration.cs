using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPatientSelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_exercises_asp_net_users_UserId",
                table: "user_exercises");

            migrationBuilder.DropTable(
                name: "doctor_patients");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "user_exercises",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_user_exercises_UserId_IsActive",
                table: "user_exercises",
                newName: "IX_user_exercises_PatientId_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_user_exercises_UserId",
                table: "user_exercises",
                newName: "IX_user_exercises_PatientId");

            migrationBuilder.RenameIndex(
                name: "ix_user_exercises_user_id_exercise_id_active",
                table: "user_exercises",
                newName: "ix_user_exercises_patient_id_exercise_id_active");

            migrationBuilder.AddColumn<Guid>(
                name: "DoctorId",
                table: "user_exercises",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_user_exercises_DoctorId",
                table: "user_exercises",
                column: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_exercises_asp_net_users_DoctorId",
                table: "user_exercises",
                column: "DoctorId",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_exercises_asp_net_users_PatientId",
                table: "user_exercises",
                column: "PatientId",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_exercises_asp_net_users_DoctorId",
                table: "user_exercises");

            migrationBuilder.DropForeignKey(
                name: "FK_user_exercises_asp_net_users_PatientId",
                table: "user_exercises");

            migrationBuilder.DropIndex(
                name: "IX_user_exercises_DoctorId",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "user_exercises");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "user_exercises",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_user_exercises_PatientId_IsActive",
                table: "user_exercises",
                newName: "IX_user_exercises_UserId_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_user_exercises_PatientId",
                table: "user_exercises",
                newName: "IX_user_exercises_UserId");

            migrationBuilder.RenameIndex(
                name: "ix_user_exercises_patient_id_exercise_id_active",
                table: "user_exercises",
                newName: "ix_user_exercises_user_id_exercise_id_active");

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

            migrationBuilder.AddForeignKey(
                name: "FK_user_exercises_asp_net_users_UserId",
                table: "user_exercises",
                column: "UserId",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
