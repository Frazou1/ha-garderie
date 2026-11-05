using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegieTexte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegieModifieeLe",
                table: "Parametres",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegieTexte",
                table: "Parametres",
                type: "TEXT",
                maxLength: 60000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegieModifieeLe",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "RegieTexte",
                table: "Parametres");
        }
    }
}
