using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEF_Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class MakeColourHexConstraintPortable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Colours_HexCode_Format",
                table: "Colours");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Colours_HexCode_Format",
                table: "Colours",
                sql: "\"HexCode\" IS NULL OR (length(\"HexCode\") = 7 AND substr(\"HexCode\", 1, 1) = '#')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Colours_HexCode_Format",
                table: "Colours");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Colours_HexCode_Format",
                table: "Colours",
                sql: "\"HexCode\" IS NULL OR \"HexCode\" ~ '^#[0-9A-Fa-f]{6}$'");
        }
    }
}
