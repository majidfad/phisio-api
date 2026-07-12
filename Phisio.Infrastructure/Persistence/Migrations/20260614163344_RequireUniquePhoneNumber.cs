using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireUniquePhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE asp_net_users
                SET "PhoneNumber" = '+10000000000',
                    "UserName" = '+10000000000',
                    "NormalizedUserName" = '+10000000000'
                WHERE "Email" = 'admin@phisio.com'
                  AND ("PhoneNumber" IS NULL OR "PhoneNumber" = '');
                """);

            migrationBuilder.Sql("""
                UPDATE asp_net_users
                SET "PhoneNumber" = "UserName"
                WHERE ("PhoneNumber" IS NULL OR "PhoneNumber" = '')
                  AND "UserName" IS NOT NULL
                  AND "UserName" !~ '@';
                """);

            migrationBuilder.Sql("""
                UPDATE asp_net_users
                SET "PhoneNumber" = '+1' || right(replace("Id"::text, '-', ''), 10),
                    "UserName" = '+1' || right(replace("Id"::text, '-', ''), 10),
                    "NormalizedUserName" = upper('+1' || right(replace("Id"::text, '-', ''), 10))
                WHERE "PhoneNumber" IS NULL OR "PhoneNumber" = '';
                """);

            migrationBuilder.DropIndex(
                name: "IX_asp_net_users_PhoneNumber",
                table: "asp_net_users");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "asp_net_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_users_PhoneNumber",
                table: "asp_net_users",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_asp_net_users_PhoneNumber",
                table: "asp_net_users");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "asp_net_users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_users_PhoneNumber",
                table: "asp_net_users",
                column: "PhoneNumber",
                unique: true,
                filter: "\"PhoneNumber\" IS NOT NULL");
        }
    }
}
