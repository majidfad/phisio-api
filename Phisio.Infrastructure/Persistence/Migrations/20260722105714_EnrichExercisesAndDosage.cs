using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrichExercisesAndDosage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClinicianNote",
                table: "user_exercises",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HoldSeconds",
                table: "user_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientCue",
                table: "user_exercises",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reps",
                table: "user_exercises",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "user_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sets",
                table: "user_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Side",
                table: "user_exercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "VideoUrl",
                table: "exercises",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BodyRegion",
                table: "exercises",
                type: "integer",
                nullable: false,
                defaultValue: 9);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByDoctorId",
                table: "exercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "exercises",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "Equipment",
                table: "exercises",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "exercises",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsClinicShared",
                table: "exercises",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "exercises",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_exercises_CreatedByDoctorId",
                table: "exercises",
                column: "CreatedByDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_exercises_IsClinicShared_IsEnabled",
                table: "exercises",
                columns: new[] { "IsClinicShared", "IsEnabled" });

            migrationBuilder.AddForeignKey(
                name: "FK_exercises_asp_net_users_CreatedByDoctorId",
                table: "exercises",
                column: "CreatedByDoctorId",
                principalTable: "asp_net_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_exercises_asp_net_users_CreatedByDoctorId",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_exercises_CreatedByDoctorId",
                table: "exercises");

            migrationBuilder.DropIndex(
                name: "IX_exercises_IsClinicShared_IsEnabled",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "ClinicianNote",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "HoldSeconds",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "PatientCue",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "Reps",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "Sets",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "BodyRegion",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "CreatedByDoctorId",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "Equipment",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "IsClinicShared",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "exercises");

            migrationBuilder.AlterColumn<string>(
                name: "VideoUrl",
                table: "exercises",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
