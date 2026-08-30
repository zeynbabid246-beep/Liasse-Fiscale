using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LiasseFiscale.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contribuables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroMatriculeFiscal = table.Column<string>(type: "text", nullable: false),
                    CleMatriculeFiscal = table.Column<string>(type: "text", nullable: false),
                    NomOuRaisonSociale = table.Column<string>(type: "text", nullable: false),
                    Activite = table.Column<string>(type: "text", nullable: false),
                    Adresse = table.Column<string>(type: "text", nullable: false),
                    Categorie = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contribuables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Liasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContribuableId = table.Column<int>(type: "integer", nullable: false),
                    Exercice = table.Column<int>(type: "integer", nullable: false),
                    DateDebutExercice = table.Column<DateOnly>(type: "date", nullable: false),
                    DateClotureExercice = table.Column<DateOnly>(type: "date", nullable: false),
                    Nature = table.Column<int>(type: "integer", nullable: false),
                    ActeDeDepot = table.Column<string>(type: "text", nullable: false),
                    TypeDepot = table.Column<string>(type: "text", nullable: false),
                    ModeleF6004Choisi = table.Column<string>(type: "text", nullable: false),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Liasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Liasses_Contribuables_ContribuableId",
                        column: x => x.ContribuableId,
                        principalTable: "Contribuables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserContribuables",
                columns: table => new
                {
                    ContribuablesId = table.Column<int>(type: "integer", nullable: false),
                    UtilisateursId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContribuables", x => new { x.ContribuablesId, x.UtilisateursId });
                    table.ForeignKey(
                        name: "FK_UserContribuables_Contribuables_ContribuablesId",
                        column: x => x.ContribuablesId,
                        principalTable: "Contribuables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserContribuables_Users_UtilisateursId",
                        column: x => x.UtilisateursId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deposits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LiasseId = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    DateDepot = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deposits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deposits_Liasses_LiasseId",
                        column: x => x.LiasseId,
                        principalTable: "Liasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LiasseId = table.Column<int>(type: "integer", nullable: false),
                    CodeDocument = table.Column<string>(type: "text", nullable: false),
                    NomFichier = table.Column<string>(type: "text", nullable: false),
                    CheminStockage = table.Column<string>(type: "text", nullable: false),
                    Statut = table.Column<string>(type: "text", nullable: false),
                    DateUpload = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Liasses_LiasseId",
                        column: x => x.LiasseId,
                        principalTable: "Liasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DepositId = table.Column<int>(type: "integer", nullable: false),
                    CheminFichier = table.Column<string>(type: "text", nullable: false),
                    DateGeneration = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_Deposits_DepositId",
                        column: x => x.DepositId,
                        principalTable: "Deposits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ValidationErrors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentFiscalId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Champ = table.Column<string>(type: "text", nullable: true),
                    Ligne = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValidationErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ValidationErrors_Documents_DocumentFiscalId",
                        column: x => x.DocumentFiscalId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contribuables_NumeroMatriculeFiscal_CleMatriculeFiscal",
                table: "Contribuables",
                columns: new[] { "NumeroMatriculeFiscal", "CleMatriculeFiscal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deposits_LiasseId",
                table: "Deposits",
                column: "LiasseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deposits_Reference",
                table: "Deposits",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_LiasseId",
                table: "Documents",
                column: "LiasseId");

            migrationBuilder.CreateIndex(
                name: "IX_Liasses_ContribuableId_Exercice",
                table: "Liasses",
                columns: new[] { "ContribuableId", "Exercice" });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_DepositId",
                table: "Receipts",
                column: "DepositId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserContribuables_UtilisateursId",
                table: "UserContribuables",
                column: "UtilisateursId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValidationErrors_DocumentFiscalId",
                table: "ValidationErrors",
                column: "DocumentFiscalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "UserContribuables");

            migrationBuilder.DropTable(
                name: "ValidationErrors");

            migrationBuilder.DropTable(
                name: "Deposits");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Liasses");

            migrationBuilder.DropTable(
                name: "Contribuables");
        }
    }
}
