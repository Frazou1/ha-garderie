using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Contrats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContratAttestation",
                table: "Parametres",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratMentionLpc",
                table: "Parametres",
                type: "TEXT",
                maxLength: 6000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratNonRespect",
                table: "Parametres",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratNoteVacances",
                table: "Parametres",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratObligationsParent",
                table: "Parametres",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratObligationsRsge",
                table: "Parametres",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VilleSignature",
                table: "Parametres",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Contrats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    Debut = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Fin = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Etat = table.Column<int>(type: "INTEGER", nullable: false),
                    VacancesParent = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Fige = table.Column<string>(type: "TEXT", nullable: true),
                    NasChiffre = table.Column<byte[]>(type: "BLOB", nullable: true),
                    SignatureParentFichier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SigneParentLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignataireParent = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IpParent = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SigneSurPlace = table.Column<bool>(type: "INTEGER", nullable: false),
                    SignatureRsgeFichier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SigneRsgeLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SigneRsgePar = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    ResilieLe = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SignatureResiliationFichier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ResiliationSigneeLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignataireResiliation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IpResiliation = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DocumentResiliationId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contrats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contrats_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LiensContrats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContratId = table.Column<int>(type: "INTEGER", nullable: false),
                    But = table.Column<int>(type: "INTEGER", nullable: false),
                    JetonHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreeLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpireLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UtiliseLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AnnuleLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EnvoyeA = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SurPlace = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiensContrats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiensContrats_Contrats_ContratId",
                        column: x => x.ContratId,
                        principalTable: "Contrats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contrats_EnfantId_Debut",
                table: "Contrats",
                columns: new[] { "EnfantId", "Debut" });

            migrationBuilder.CreateIndex(
                name: "IX_LiensContrats_ContratId",
                table: "LiensContrats",
                column: "ContratId");

            migrationBuilder.CreateIndex(
                name: "IX_LiensContrats_JetonHash",
                table: "LiensContrats",
                column: "JetonHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiensContrats");

            migrationBuilder.DropTable(
                name: "Contrats");

            migrationBuilder.DropColumn(
                name: "ContratAttestation",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratMentionLpc",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratNonRespect",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratNoteVacances",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratObligationsParent",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratObligationsRsge",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "VilleSignature",
                table: "Parametres");
        }
    }
}
