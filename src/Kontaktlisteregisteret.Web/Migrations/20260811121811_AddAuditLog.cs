using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kontaktlisteregisteret.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogg",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tidspunkt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Handling = table.Column<string>(type: "TEXT", nullable: false),
                    EnhetsType = table.Column<string>(type: "TEXT", nullable: false),
                    EnhetsId = table.Column<int>(type: "INTEGER", nullable: true),
                    EnhetsNavn = table.Column<string>(type: "TEXT", nullable: true),
                    VirksomhetId = table.Column<int>(type: "INTEGER", nullable: true),
                    UtførtAv = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogg", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogg_Tidspunkt",
                table: "AuditLogg",
                column: "Tidspunkt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogg_VirksomhetId",
                table: "AuditLogg",
                column: "VirksomhetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogg");
        }
    }
}
