using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderRepeatAndFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ReminderAnchorDate",
                table: "asp_net_users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderDaysOfWeekMask",
                table: "asp_net_users",
                type: "integer",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddColumn<bool>(
                name: "ReminderFollowUpEnabled",
                table: "asp_net_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ReminderFollowUpTime",
                table: "asp_net_users",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(18, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "ReminderIntervalDays",
                table: "asp_net_users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ReminderRepeatMode",
                table: "asp_net_users",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderAnchorDate",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "ReminderDaysOfWeekMask",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "ReminderFollowUpEnabled",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "ReminderFollowUpTime",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "ReminderIntervalDays",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "ReminderRepeatMode",
                table: "asp_net_users");
        }
    }
}
