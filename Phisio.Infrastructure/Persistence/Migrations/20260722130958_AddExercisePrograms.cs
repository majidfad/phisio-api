using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExercisePrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "user_exercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "exercise_programs",
                columns: table => new
                {
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CadenceType = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    DaysOfWeekMask = table.Column<int>(type: "integer", nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_programs", x => x.ProgramId);
                    table.ForeignKey(
                        name: "FK_exercise_programs_asp_net_users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_exercise_programs_asp_net_users_PatientId",
                        column: x => x.PatientId,
                        principalTable: "asp_net_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "program_exercises",
                columns: table => new
                {
                    ProgramExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sets = table.Column<int>(type: "integer", nullable: true),
                    Reps = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HoldSeconds = table.Column<int>(type: "integer", nullable: true),
                    RestSeconds = table.Column<int>(type: "integer", nullable: true),
                    Side = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ClinicianNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PatientCue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_exercises", x => x.ProgramExerciseId);
                    table.ForeignKey(
                        name: "FK_program_exercises_exercise_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "exercise_programs",
                        principalColumn: "ProgramId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_exercises_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "ExerciseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_exercises_ProgramId",
                table: "user_exercises",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_programs_DoctorId_PatientId",
                table: "exercise_programs",
                columns: new[] { "DoctorId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_programs_PatientId",
                table: "exercise_programs",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_program_exercises_ExerciseId",
                table: "program_exercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "ix_program_exercises_program_exercise_enabled",
                table: "program_exercises",
                columns: new[] { "ProgramId", "ExerciseId" },
                unique: true,
                filter: "\"IsEnabled\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_program_exercises_ProgramId",
                table: "program_exercises",
                column: "ProgramId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_exercises_exercise_programs_ProgramId",
                table: "user_exercises",
                column: "ProgramId",
                principalTable: "exercise_programs",
                principalColumn: "ProgramId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_exercises_exercise_programs_ProgramId",
                table: "user_exercises");

            migrationBuilder.DropTable(
                name: "program_exercises");

            migrationBuilder.DropTable(
                name: "exercise_programs");

            migrationBuilder.DropIndex(
                name: "IX_user_exercises_ProgramId",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "user_exercises");
        }
    }
}
