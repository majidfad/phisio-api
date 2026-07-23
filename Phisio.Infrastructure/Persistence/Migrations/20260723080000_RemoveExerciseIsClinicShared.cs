using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Phisio.Infrastructure.Persistence;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260723080000_RemoveExerciseIsClinicShared")]
public partial class RemoveExerciseIsClinicShared : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_exercises_IsClinicShared_IsEnabled",
            table: "exercises");

        migrationBuilder.DropColumn(
            name: "IsClinicShared",
            table: "exercises");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsClinicShared",
            table: "exercises",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateIndex(
            name: "IX_exercises_IsClinicShared_IsEnabled",
            table: "exercises",
            columns: new[] { "IsClinicShared", "IsEnabled" });
    }
}
