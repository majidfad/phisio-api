using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientReminderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExerciseRemindersEnabled",
                table: "asp_net_users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PreferredReminderTime",
                table: "asp_net_users",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(9, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "asp_net_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Asia/Tehran");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExerciseRemindersEnabled",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "PreferredReminderTime",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "asp_net_users");
        }
    }
}
