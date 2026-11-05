using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Releves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Releves",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Annee = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentId = table.Column<int>(type: "INTEGER", nullable: true),
                    Numero = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 1, nullable: false),
                    NumeroOriginal = table.Column<string>(type: "TEXT", maxLength: 9, nullable: true),
                    Contenu = table.Column<string>(type: "TEXT", nullable: false),
                    EnfantId = table.Column<int>(type: "INTEGER", nullable: false),
                    DocumentId = table.Column<int>(type: "INTEGER", nullable: true),
                    RemplaceParId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreeLe = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreePar = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EnvoyeLe = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EnvoyeA = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Releves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Releves_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Releves_Annee_ParentId",
                table: "Releves",
                columns: new[] { "Annee", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Releves_Numero",
                table: "Releves",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Releves_ParentId",
                table: "Releves",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Releves");
        }
    }
}
