using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class PresenceParJourEtPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Une case par jour : si matin et apres-midi existent, le code du matin est garde.
            // One cell per day: when both halves exist, the morning code is kept.
            migrationBuilder.Sql("DELETE FROM Presences WHERE Demi = 1 AND EXISTS (SELECT 1 FROM Presences m WHERE m.EnfantId = Presences.EnfantId AND m.Date = Presences.Date AND m.Demi = 0);");

            migrationBuilder.DropIndex(
                name: "IX_Presences_EnfantId_Date_Demi",
                table: "Presences");

            migrationBuilder.DropColumn(
                name: "Demi",
                table: "Presences");

            migrationBuilder.AddColumn<string>(
                name: "PhotoFichier",
                table: "Parents",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoFichier",
                table: "Contacts",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Presences_EnfantId_Date",
                table: "Presences",
                columns: new[] { "EnfantId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Presences_EnfantId_Date",
                table: "Presences");

            migrationBuilder.DropColumn(
                name: "PhotoFichier",
                table: "Parents");

            migrationBuilder.DropColumn(
                name: "PhotoFichier",
                table: "Contacts");

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
        }
    }
}
