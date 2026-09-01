using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LiasseFiscale.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_AuthorizationAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Liasses_Contribuables_ContribuableId",
                table: "Liasses");

            migrationBuilder.DropTable(
                name: "UserContribuables");

            migrationBuilder.DropIndex(
                name: "IX_Liasses_ContribuableId_Exercice",
                table: "Liasses");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginIp",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateReview",
                table: "Liasses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateSubmission",
                table: "Liasses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "Liasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedBy",
                table: "Liasses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmittedBy",
                table: "Liasses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChecksumSha256",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UploadedBy",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Contribuables",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    ContribuableId = table.Column<int>(type: "integer", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Contribuables_ContribuableId",
                        column: x => x.ContribuableId,
                        principalTable: "Contribuables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserCompanyAuthorizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ContribuableId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    MandateReference = table.Column<string>(type: "text", nullable: true),
                    DateAuthorized = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateExpired = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCompanyAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCompanyAuthorizations_Contribuables_ContribuableId",
                        column: x => x.ContribuableId,
                        principalTable: "Contribuables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCompanyAuthorizations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Liasse_StatusLookup",
                table: "Liasses",
                columns: new[] { "ContribuableId", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_Liasse_UniqueContext",
                table: "Liasses",
                columns: new[] { "ContribuableId", "Exercice", "ActeDeDepot" });

            migrationBuilder.CreateIndex(
                name: "IX_Liasses_ReviewedBy",
                table: "Liasses",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Liasses_SubmittedBy",
                table: "Liasses",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedBy",
                table: "Documents",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Contribuables_UserId",
                table: "Contribuables",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ContribuableTimestamp",
                table: "AuditLogs",
                columns: new[] { "ContribuableId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_UserTimestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyAuthorization_Unique",
                table: "UserCompanyAuthorizations",
                columns: new[] { "UserId", "ContribuableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanyAuthorizations_ContribuableId",
                table: "UserCompanyAuthorizations",
                column: "ContribuableId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contribuables_Users_UserId",
                table: "Contribuables",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UploadedBy",
                table: "Documents",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Liasses_Contribuables_ContribuableId",
                table: "Liasses",
                column: "ContribuableId",
                principalTable: "Contribuables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Liasses_Users_ReviewedBy",
                table: "Liasses",
                column: "ReviewedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Liasses_Users_SubmittedBy",
                table: "Liasses",
                column: "SubmittedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contribuables_Users_UserId",
                table: "Contribuables");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UploadedBy",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Liasses_Contribuables_ContribuableId",
                table: "Liasses");

            migrationBuilder.DropForeignKey(
                name: "FK_Liasses_Users_ReviewedBy",
                table: "Liasses");

            migrationBuilder.DropForeignKey(
                name: "FK_Liasses_Users_SubmittedBy",
                table: "Liasses");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "UserCompanyAuthorizations");

            migrationBuilder.DropIndex(
                name: "IX_Liasse_StatusLookup",
                table: "Liasses");

            migrationBuilder.DropIndex(
                name: "IX_Liasse_UniqueContext",
                table: "Liasses");

            migrationBuilder.DropIndex(
                name: "IX_Liasses_ReviewedBy",
                table: "Liasses");

            migrationBuilder.DropIndex(
                name: "IX_Liasses_SubmittedBy",
                table: "Liasses");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UploadedBy",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Contribuables_UserId",
                table: "Contribuables");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginIp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DateReview",
                table: "Liasses");

            migrationBuilder.DropColumn(
                name: "DateSubmission",
                table: "Liasses");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "Liasses");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "Liasses");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "Liasses");

            migrationBuilder.DropColumn(
                name: "ChecksumSha256",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UploadedBy",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Contribuables");

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

            migrationBuilder.CreateIndex(
                name: "IX_Liasses_ContribuableId_Exercice",
                table: "Liasses",
                columns: new[] { "ContribuableId", "Exercice" });

            migrationBuilder.CreateIndex(
                name: "IX_UserContribuables_UtilisateursId",
                table: "UserContribuables",
                column: "UtilisateursId");

            migrationBuilder.AddForeignKey(
                name: "FK_Liasses_Contribuables_ContribuableId",
                table: "Liasses",
                column: "ContribuableId",
                principalTable: "Contribuables",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
