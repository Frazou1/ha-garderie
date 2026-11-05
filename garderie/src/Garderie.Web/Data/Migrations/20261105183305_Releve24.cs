using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Releve24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rl24Appartement",
                table: "Parametres",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rl24CodePostal",
                table: "Parametres",
                type: "TEXT",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rl24DernierNumero",
                table: "Parametres",
                type: "TEXT",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Rl24IdentifiantChiffre",
                table: "Parametres",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rl24Numero",
                table: "Parametres",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rl24Province",
                table: "Parametres",
                type: "TEXT",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rl24Rue",
                table: "Parametres",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rl24Signataire",
                table: "Parametres",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rl24Ville",
                table: "Parametres",
                type: "TEXT",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rl24Appartement",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24CodePostal",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24DernierNumero",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24IdentifiantChiffre",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24Numero",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24Province",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24Rue",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24Signataire",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "Rl24Ville",
                table: "Parametres");
        }
    }
}
