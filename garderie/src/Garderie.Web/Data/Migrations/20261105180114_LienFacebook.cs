using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garderie.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class LienFacebook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LienFacebook",
                table: "Parametres",
                type: "TEXT",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LienFacebook",
                table: "Parametres");
        }
    }
}
