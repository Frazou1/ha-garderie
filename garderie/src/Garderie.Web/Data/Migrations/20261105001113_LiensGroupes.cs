using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiensGroupes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiensFiches",
                columns: table => new
                {
                    LienId = table.Column<int>(type: "INTEGER", nullable: false),
                    FicheId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiensFiches", x => new { x.LienId, x.FicheId });
                    table.ForeignKey(
                        name: "FK_LiensFiches_Fiches_FicheId",
                        column: x => x.FicheId,
                        principalTable: "Fiches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiensFiches_Liens_LienId",
                        column: x => x.LienId,
                        principalTable: "Liens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiensFiches_FicheId",
                table: "LiensFiches",
                column: "FicheId");

            // Liens existants : leur fiche devient le groupe / Existing links: their sheet becomes the group
            migrationBuilder.Sql("INSERT INTO LiensFiches (LienId, FicheId) SELECT Id, FicheId FROM Liens;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiensFiches");
        }
    }
}
