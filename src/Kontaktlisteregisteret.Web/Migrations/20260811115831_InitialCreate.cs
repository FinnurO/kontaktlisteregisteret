using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kontaktlisteregisteret.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    OrgForm = table.Column<string>(type: "TEXT", nullable: true),
                    NaceCode = table.Column<string>(type: "TEXT", nullable: true),
                    PostalAddress = table.Column<string>(type: "TEXT", nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", nullable: true),
                    PostalCity = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Virksomheter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Orgnr = table.Column<string>(type: "TEXT", nullable: false),
                    Navn = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OnboardetAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OnboardetAv = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Virksomheter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Abonnementslister",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Navn = table.Column<string>(type: "TEXT", nullable: false),
                    Beskrivelse = table.Column<string>(type: "TEXT", nullable: true),
                    OpprettetAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OpprettetAv = table.Column<string>(type: "TEXT", nullable: true),
                    VirksomhetId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abonnementslister", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Abonnementslister_Virksomheter_VirksomhetId",
                        column: x => x.VirksomhetId,
                        principalTable: "Virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Adresselister",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tittel = table.Column<string>(type: "TEXT", nullable: false),
                    Beskrivelse = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OpprettetAv = table.Column<string>(type: "TEXT", nullable: true),
                    OpprettetAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LåstAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EkskluderteJson = table.Column<string>(type: "TEXT", nullable: true),
                    VirksomhetId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adresselister", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adresselister_Virksomheter_VirksomhetId",
                        column: x => x.VirksomhetId,
                        principalTable: "Virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TargetGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    DynamicCriteriaJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VirksomhetId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetGroups_Virksomheter_VirksomhetId",
                        column: x => x.VirksomhetId,
                        principalTable: "Virksomheter",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Abonnenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AbonnementslisteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Epost = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    LagtTilAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kilde = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abonnenter", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Abonnenter_Abonnementslister_AbonnementslisteId",
                        column: x => x.AbonnementslisteId,
                        principalTable: "Abonnementslister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdresselisteAbonnementslister",
                columns: table => new
                {
                    AdresselisteId = table.Column<int>(type: "INTEGER", nullable: false),
                    AbonnementslisteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rekkefølge = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdresselisteAbonnementslister", x => new { x.AdresselisteId, x.AbonnementslisteId });
                    table.ForeignKey(
                        name: "FK_AdresselisteAbonnementslister_Abonnementslister_AbonnementslisteId",
                        column: x => x.AbonnementslisteId,
                        principalTable: "Abonnementslister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdresselisteAbonnementslister_Adresselister_AdresselisteId",
                        column: x => x.AdresselisteId,
                        principalTable: "Adresselister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdresselisteMottakere",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AdresselisteId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientId = table.Column<int>(type: "INTEGER", nullable: false),
                    KildeMålgruppeId = table.Column<int>(type: "INTEGER", nullable: true),
                    KildeAbonnementslisteId = table.Column<int>(type: "INTEGER", nullable: true),
                    Visningsnavn = table.Column<string>(type: "TEXT", nullable: true),
                    CoAdresse = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdresselisteMottakere", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdresselisteMottakere_Adresselister_AdresselisteId",
                        column: x => x.AdresselisteId,
                        principalTable: "Adresselister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdresselisteMottakere_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdresselisteMålgrupper",
                columns: table => new
                {
                    AdresselisteId = table.Column<int>(type: "INTEGER", nullable: false),
                    MålgruppeId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rekkefølge = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdresselisteMålgrupper", x => new { x.AdresselisteId, x.MålgruppeId });
                    table.ForeignKey(
                        name: "FK_AdresselisteMålgrupper_Adresselister_AdresselisteId",
                        column: x => x.AdresselisteId,
                        principalTable: "Adresselister",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdresselisteMålgrupper_TargetGroups_MålgruppeId",
                        column: x => x.MålgruppeId,
                        principalTable: "TargetGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TargetGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipientId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Visningsnavn = table.Column<string>(type: "TEXT", nullable: true),
                    CoAdresse = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TargetGroupMembers_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TargetGroupMembers_TargetGroups_TargetGroupId",
                        column: x => x.TargetGroupId,
                        principalTable: "TargetGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Abonnementslister_VirksomhetId",
                table: "Abonnementslister",
                column: "VirksomhetId");

            migrationBuilder.CreateIndex(
                name: "IX_Abonnenter_AbonnementslisteId_Epost",
                table: "Abonnenter",
                columns: new[] { "AbonnementslisteId", "Epost" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdresselisteAbonnementslister_AbonnementslisteId",
                table: "AdresselisteAbonnementslister",
                column: "AbonnementslisteId");

            migrationBuilder.CreateIndex(
                name: "IX_AdresselisteMottakere_AdresselisteId",
                table: "AdresselisteMottakere",
                column: "AdresselisteId");

            migrationBuilder.CreateIndex(
                name: "IX_AdresselisteMottakere_RecipientId",
                table: "AdresselisteMottakere",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_AdresselisteMålgrupper_MålgruppeId",
                table: "AdresselisteMålgrupper",
                column: "MålgruppeId");

            migrationBuilder.CreateIndex(
                name: "IX_Adresselister_Status",
                table: "Adresselister",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Adresselister_VirksomhetId",
                table: "Adresselister",
                column: "VirksomhetId");

            migrationBuilder.CreateIndex(
                name: "IX_Recipients_ExternalId",
                table: "Recipients",
                column: "ExternalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TargetGroupMembers_RecipientId",
                table: "TargetGroupMembers",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetGroupMembers_TargetGroupId_RecipientId",
                table: "TargetGroupMembers",
                columns: new[] { "TargetGroupId", "RecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_TargetGroups_Name",
                table: "TargetGroups",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TargetGroups_VirksomhetId",
                table: "TargetGroups",
                column: "VirksomhetId");

            migrationBuilder.CreateIndex(
                name: "IX_Virksomheter_Orgnr",
                table: "Virksomheter",
                column: "Orgnr",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Abonnenter");

            migrationBuilder.DropTable(
                name: "AdresselisteAbonnementslister");

            migrationBuilder.DropTable(
                name: "AdresselisteMottakere");

            migrationBuilder.DropTable(
                name: "AdresselisteMålgrupper");

            migrationBuilder.DropTable(
                name: "TargetGroupMembers");

            migrationBuilder.DropTable(
                name: "Abonnementslister");

            migrationBuilder.DropTable(
                name: "Adresselister");

            migrationBuilder.DropTable(
                name: "Recipients");

            migrationBuilder.DropTable(
                name: "TargetGroups");

            migrationBuilder.DropTable(
                name: "Virksomheter");
        }
    }
}
