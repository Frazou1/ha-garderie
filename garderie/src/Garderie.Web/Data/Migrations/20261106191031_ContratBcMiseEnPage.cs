using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ContratBcMiseEnPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContratFraisNsf",
                table: "Parametres",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratRetardMinutes",
                table: "Parametres",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContratRetardMontant",
                table: "Parametres",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContratFraisNsf",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratRetardMinutes",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ContratRetardMontant",
                table: "Parametres");
        }
    }
}
