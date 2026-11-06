using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModeleBureauCoordonnateur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContratClausesFermeture",
                table: "Parametres",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratResiliationParent",
                table: "Parametres",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratResiliationRsge",
                table: "Parametres",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratServices",
                table: "Parametres",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeureCollationMatin",
                table: "Parametres",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeureCollationPm",
                table: "Parametres",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeureDiner",
                table: "Parametres",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegieModesPaiement",
                table: "Parametres",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroPortail",
                table: "Enfants",
                type: "TEXT",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContratClausesFermeture",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratResiliationParent",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratResiliationRsge",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratServices",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "HeureCollationMatin",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "HeureCollationPm",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "HeureDiner",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "RegieModesPaiement",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "NumeroPortail",
                table: "Enfants");
        }
    }
}
