using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PresenceDemiJournee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Presences_EnfantId_Date",
                table: "Presences");

            migrationBuilder.AddColumn<int>(
                name: "Demi",
                table: "Presences",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Presences_EnfantId_Date_Demi",
                table: "Presences",
                columns: new[] { "EnfantId", "Date", "Demi" },
                unique: true);

            // Une case par jour -> 2 demi-cases : le code va au matin (defaut 0) et est copie l'apres-midi
            // (journee complete = P dans les 2 cases). / One cell per day -> morning + copy to afternoon.
            migrationBuilder.Sql("INSERT INTO Presences (EnfantId, Date, Demi, Code, ModifieLe, ModifiePar) " +
                "SELECT EnfantId, Date, 1, Code, ModifieLe, ModifiePar FROM Presences WHERE Demi = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Presences_EnfantId_Date_Demi",
                table: "Presences");

            migrationBuilder.DropColumn(
                name: "Demi",
                table: "Presences");

            migrationBuilder.CreateIndex(
                name: "IX_Presences_EnfantId_Date",
                table: "Presences",
                columns: new[] { "EnfantId", "Date" },
                unique: true);
        }
    }
}
