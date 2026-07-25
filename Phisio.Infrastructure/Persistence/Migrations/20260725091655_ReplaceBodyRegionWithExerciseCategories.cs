using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceBodyRegionWithExerciseCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyRegion",
                table: "exercises");

            migrationBuilder.CreateTable(
                name: "exercise_categories",
                columns: table => new
                {
                    ExerciseCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameFa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_categories", x => x.ExerciseCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "exercise_category_links",
                columns: table => new
                {
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercise_category_links", x => new { x.ExerciseId, x.ExerciseCategoryId });
                    table.ForeignKey(
                        name: "FK_exercise_category_links_exercise_categories_ExerciseCategor~",
                        column: x => x.ExerciseCategoryId,
                        principalTable: "exercise_categories",
                        principalColumn: "ExerciseCategoryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_exercise_category_links_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "ExerciseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exercise_categories_NameEn",
                table: "exercise_categories",
                column: "NameEn");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_categories_NameFa",
                table: "exercise_categories",
                column: "NameFa");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_categories_SortOrder",
                table: "exercise_categories",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_exercise_category_links_ExerciseCategoryId",
                table: "exercise_category_links",
                column: "ExerciseCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exercise_category_links");

            migrationBuilder.DropTable(
                name: "exercise_categories");

            migrationBuilder.AddColumn<int>(
                name: "BodyRegion",
                table: "exercises",
                type: "integer",
                nullable: false,
                defaultValue: 9);
        }
    }
}
