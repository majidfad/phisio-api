using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitTypeAndPatientCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "PatientCondition",
                table: "patient_visits",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "VisitType",
                table: "patient_visits",
                type: "smallint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PatientCondition",
                table: "patient_visits");

            migrationBuilder.DropColumn(
                name: "VisitType",
                table: "patient_visits");
        }
    }
}
