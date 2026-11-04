using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FichesAssiduite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DebutPeriodes",
                table: "Parametres",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fiches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Debut = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Etat = table.Column<int>(type: "INTEGER", nullable: false),
                    DatePaiement1 = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    MontantPaye1 = table.Column<decimal>(type: "TEXT", nullable: true),
                    Solde1 = table.Column<decimal>(type: "TEXT", nullable: true),
                    DatePaiement2 = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    MontantPaye2 = table.Column<decimal>(type: "TEXT", nullable: true),
                    Solde2 = table.Column<decimal>(type: "TEXT", nullable: true),
                    CodesSignes = table.Column<string>(type: "TEXT", nullable: true),
                    NomParent = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    SignatureRsgeFichier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SigneRsgeLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SigneRsgePar = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SignatureParentFichier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SigneParentLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignataireParent = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IpParent = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SigneSurPlace = table.Column<bool>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    PdfSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ErreurSignalee = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ErreurSignaleeLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fiches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fiches_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Liens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FicheId = table.Column<int>(type: "INTEGER", nullable: false),
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
                    table.PrimaryKey("PK_Liens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Liens_Fiches_FicheId",
                        column: x => x.FicheId,
                        principalTable: "Fiches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fiches_EnfantId_Debut",
                table: "Fiches",
                columns: new[] { "EnfantId", "Debut" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Liens_FicheId",
                table: "Liens",
                column: "FicheId");

            migrationBuilder.CreateIndex(
                name: "IX_Liens_JetonHash",
                table: "Liens",
                column: "JetonHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Liens");

            migrationBuilder.DropTable(
                name: "Fiches");

            migrationBuilder.DropColumn(
                name: "DebutPeriodes",
                table: "Parametres");
        }
    }
}
