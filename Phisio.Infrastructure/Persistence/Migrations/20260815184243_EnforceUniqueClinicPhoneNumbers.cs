using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUniqueClinicPhoneNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedPhoneNumber",
                table: "clinic_phone_numbers",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE clinic_phone_numbers
                SET "NormalizedPhoneNumber" =
                    '+' || regexp_replace("PhoneNumber", '[^0-9]', '', 'g');

                ALTER TABLE clinic_phone_numbers
                ALTER COLUMN "NormalizedPhoneNumber" DROP DEFAULT;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM clinic_phone_numbers
                        GROUP BY "NormalizedPhoneNumber"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Duplicate normalized clinic phone numbers must be resolved before this migration can complete.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_clinic_phone_numbers_NormalizedPhoneNumber",
                table: "clinic_phone_numbers",
                column: "NormalizedPhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_clinic_phone_numbers_NormalizedPhoneNumber",
                table: "clinic_phone_numbers");

            migrationBuilder.DropColumn(
                name: "NormalizedPhoneNumber",
                table: "clinic_phone_numbers");
        }
    }
}
