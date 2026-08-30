using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiasseFiscale.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Observation",
                table: "Deposits",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SignatureElectronique",
                table: "Deposits",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodeCategorie",
                table: "Contribuables",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CodeTva",
                table: "Contribuables",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Observation",
                table: "Deposits");

            migrationBuilder.DropColumn(
                name: "SignatureElectronique",
                table: "Deposits");

            migrationBuilder.DropColumn(
                name: "CodeCategorie",
                table: "Contribuables");

            migrationBuilder.DropColumn(
                name: "CodeTva",
                table: "Contribuables");
        }
    }
}
