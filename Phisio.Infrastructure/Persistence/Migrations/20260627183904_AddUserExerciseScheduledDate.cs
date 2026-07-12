using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserExerciseScheduledDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_exercises_patient_id_exercise_id_active",
                table: "user_exercises");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledDate",
                table: "user_exercises",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.Sql("""
                UPDATE user_exercises
                SET "ScheduledDate" = ("AssignedAt" AT TIME ZONE 'UTC')::date
                WHERE "ScheduledDate" = DATE '0001-01-01';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_user_exercises_patient_doctor_exercise_scheduled_active",
                table: "user_exercises",
                columns: new[] { "PatientId", "DoctorId", "ExerciseId", "ScheduledDate" },
                unique: true,
                filter: "\"IsActive\" = true AND \"IsEnabled\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_exercises_patient_doctor_exercise_scheduled_active",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "user_exercises");

            migrationBuilder.CreateIndex(
                name: "ix_user_exercises_patient_id_exercise_id_active",
                table: "user_exercises",
                columns: new[] { "PatientId", "ExerciseId" },
                unique: true,
                filter: "\"IsActive\" = true");
        }
    }
}
