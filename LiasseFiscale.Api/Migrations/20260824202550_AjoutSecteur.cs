using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiasseFiscale.Api.Migrations
{
    /// <inheritdoc />
    public partial class AjoutSecteur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nature",
                table: "Liasses",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Categorie",
                table: "Liasses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EstObligatoire",
                table: "Documents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Libelle",
                table: "Documents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categorie",
                table: "Liasses");

            migrationBuilder.DropColumn(
                name: "EstObligatoire",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Libelle",
                table: "Documents");

            migrationBuilder.AlterColumn<int>(
                name: "Nature",
                table: "Liasses",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
