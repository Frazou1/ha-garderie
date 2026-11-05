using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegieSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegieTexte",
                table: "Parametres");

            migrationBuilder.AddColumn<string>(
                name: "RegieFraisRetard",
                table: "Parametres",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegieJourPaiement",
                table: "Parametres",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegieSections",
                table: "Parametres",
                type: "TEXT",
                maxLength: 100000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegieTarif",
                table: "Parametres",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegieFraisRetard",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "RegieJourPaiement",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "RegieSections",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "RegieTarif",
                table: "Parametres");

            migrationBuilder.AddColumn<string>(
                name: "RegieTexte",
                table: "Parametres",
                type: "TEXT",
                maxLength: 60000,
                nullable: true);
        }
    }
}
