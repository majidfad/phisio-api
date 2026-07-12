using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseCompletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exercise_completions",
                columns: table => new
                {
                    ExerciseCompletionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_completions", x => x.ExerciseCompletionId);
                    table.ForeignKey(
                        name: "FK_exercise_completions_user_exercises_UserExerciseId",
                        column: x => x.UserExerciseId,
                        principalTable: "user_exercises",
                        principalColumn: "UserExerciseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_CompletionDate",
                table: "exercise_completions",
                column: "CompletionDate");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_DoctorId",
                table: "exercise_completions",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_ExerciseId",
                table: "exercise_completions",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_completions_PatientId",
                table: "exercise_completions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "ix_exercise_completions_user_exercise_id_completion_date",
                table: "exercise_completions",
                columns: new[] { "UserExerciseId", "CompletionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_completions");
        }
    }
}
