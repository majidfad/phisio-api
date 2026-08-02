using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExerciseHoldRestSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoldSeconds",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "user_exercises");

            migrationBuilder.DropColumn(
                name: "HoldSeconds",
                table: "program_exercises");

            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "program_exercises");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "program_exercises");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HoldSeconds",
                table: "user_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "user_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Side",
                table: "user_exercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HoldSeconds",
                table: "program_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "program_exercises",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Side",
                table: "program_exercises",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
