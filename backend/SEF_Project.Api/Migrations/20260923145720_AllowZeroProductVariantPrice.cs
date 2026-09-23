using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEF_Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class AllowZeroProductVariantPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductVariants_Price",
                table: "ProductVariants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductVariants_Price",
                table: "ProductVariants",
                sql: "\"Price\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductVariants_Price",
                table: "ProductVariants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductVariants_Price",
                table: "ProductVariants",
                sql: "\"Price\" > 0");
        }
    }
}
