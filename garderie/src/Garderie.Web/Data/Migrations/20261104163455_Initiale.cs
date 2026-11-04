using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initiale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Enfants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prenom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DateNaissance = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Sexe = table.Column<int>(type: "INTEGER", nullable: true),
                    Adresse = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Telephone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    NamChiffre = table.Column<byte[]>(type: "BLOB", nullable: true),
                    DateDebut = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DateFin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    NotesSante = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    PhotoDocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enfants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fermetures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Demi = table.Column<int>(type: "INTEGER", nullable: true),
                    Motif = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fermetures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Journal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Horodatage = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Utilisateur = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Ip = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Entite = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EntiteId = table.Column<int>(type: "INTEGER", nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parametres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NomGarderie = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NomRsge = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Adresse = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Telephone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    CourrielRsge = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NomBureauCoordonnateur = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    HeureOuverture = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    HeureFermeture = table.Column<TimeOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prenom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Courriel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Telephone1 = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Telephone2 = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    Adresse = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    NasChiffre = table.Column<byte[]>(type: "BLOB", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Allergies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Gravite = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesures = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allergies_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Lien = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Telephone = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    PeutVenirChercher = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContactUrgence = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ordre = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    NomOriginal = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TypeContenu = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Taille = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Fichier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TeleverseLe = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Horaires",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateEffective = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Jour = table.Column<int>(type: "INTEGER", nullable: false),
                    Matin = table.Column<bool>(type: "INTEGER", nullable: false),
                    Pm = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Horaires", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Horaires_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Medicaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nom = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Posologie = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Raison = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    DateDebut = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DateFin = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicaments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Medicaments_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Presences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Demi = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    ModifieLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiePar = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Presences_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnfantParents",
                columns: table => new
                {
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: false),
                    Lien = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EstPayeur = table.Column<bool>(type: "INTEGER", nullable: false),
                    Ordre = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnfantParents", x => new { x.EnfantId, x.ParentId });
                    table.ForeignKey(
                        name: "FK_EnfantParents_Enfants_EnfantId",
                        column: x => x.EnfantId,
                        principalTable: "Enfants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnfantParents_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allergies_EnfantId",
                table: "Allergies",
                column: "EnfantId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_EnfantId",
                table: "Contacts",
                column: "EnfantId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_EnfantId",
                table: "Documents",
                column: "EnfantId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Fichier",
                table: "Documents",
                column: "Fichier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnfantParents_ParentId",
                table: "EnfantParents",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Enfants_Nom_Prenom",
                table: "Enfants",
                columns: new[] { "Nom", "Prenom" });

            migrationBuilder.CreateIndex(
                name: "IX_Fermetures_Date",
                table: "Fermetures",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Horaires_EnfantId_DateEffective_Jour",
                table: "Horaires",
                columns: new[] { "EnfantId", "DateEffective", "Jour" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Journal_Horodatage",
                table: "Journal",
                column: "Horodatage");

            migrationBuilder.CreateIndex(
                name: "IX_Medicaments_EnfantId",
                table: "Medicaments",
                column: "EnfantId");

            migrationBuilder.CreateIndex(
                name: "IX_Presences_Date",
                table: "Presences",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Presences_EnfantId_Date_Demi",
                table: "Presences",
                columns: new[] { "EnfantId", "Date", "Demi" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allergies");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "EnfantParents");

            migrationBuilder.DropTable(
                name: "Fermetures");

            migrationBuilder.DropTable(
                name: "Horaires");

            migrationBuilder.DropTable(
                name: "Journal");

            migrationBuilder.DropTable(
                name: "Medicaments");

            migrationBuilder.DropTable(
                name: "Parametres");

            migrationBuilder.DropTable(
                name: "Presences");

            migrationBuilder.DropTable(
                name: "Parents");

            migrationBuilder.DropTable(
                name: "Enfants");
        }
    }
}
