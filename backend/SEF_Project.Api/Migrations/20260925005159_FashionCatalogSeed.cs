using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SEF_Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class FashionCatalogSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "Description",
                value: "Launch promotion for the new season.");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Name",
                value: "Tops");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "Name",
                value: "Bottoms");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "Name",
                value: "Outerwear");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "Name",
                value: "Footwear");

            migrationBuilder.UpdateData(
                table: "Collections",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Lightweight staples for the warm season.", "Summer Essentials" });

            migrationBuilder.UpdateData(
                table: "Collections",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Featured pieces from the signature line.", "Signature Selection" });

            migrationBuilder.UpdateData(
                table: "Collections",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "The latest additions to the catalogue.", "New Arrivals" });

            migrationBuilder.UpdateData(
                table: "Colours",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003e"),
                columns: new[] { "HexCode", "Name" },
                values: new object[] { "#000000", "Black" });

            migrationBuilder.InsertData(
                table: "Colours",
                columns: new[] { "Id", "CreatedAt", "HexCode", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-00000000003f"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "#ffffff", true, "White", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-000000000040"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "#001f3f", true, "Navy", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"),
                columns: new[] { "Name", "Price", "Sku" },
                values: new object[] { "XS / Black", 2500m, "TSH-CLS-XS" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"),
                columns: new[] { "ColourId", "Name", "Price", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000003f"), "M / White", 2600m, "TSH-CLS-M" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000033"),
                columns: new[] { "ColourId", "Name", "Price", "SizeId", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000040"), "M / Navy", 6500m, new Guid("00000000-0000-0000-0000-00000000003a"), "HOD-FLC-M" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000034"),
                columns: new[] { "ColourId", "Name", "Price", "SizeId", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000040"), "L / Navy", 6900m, new Guid("00000000-0000-0000-0000-00000000003b"), "HOD-FLC-L" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000035"),
                columns: new[] { "Name", "Price", "SizeId", "Sku" },
                values: new object[] { "M / Black", 7500m, new Guid("00000000-0000-0000-0000-00000000003a"), "JEA-SLM-M" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000036"),
                columns: new[] { "ColourId", "Name", "Price", "SizeId", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000040"), "L / Navy", 12500m, new Guid("00000000-0000-0000-0000-00000000003b"), "JKT-QFD-L" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000037"),
                columns: new[] { "Name", "Price", "SizeId", "Sku" },
                values: new object[] { "One Size / Black", 8900m, new Guid("00000000-0000-0000-0000-00000000003d"), "BTS-ANK-OS" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Soft combed cotton crew-neck tee.", "Classic Cotton T-Shirt" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"),
                columns: new[] { "CollectionId", "Description", "Name" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), "Brushed fleece hoodie with a kangaroo pocket.", "Fleece Pullover Hoodie" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Mid-rise slim jeans in stretch denim.", "Slim Fit Denim Jeans" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"),
                columns: new[] { "CollectionId", "Description", "Name", "SupplierId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), "Lightly quilted jacket for layering.", "Quilted Field Jacket", new Guid("00000000-0000-0000-0000-000000000011") });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"),
                columns: new[] { "CollectionId", "Description", "Name", "SupplierId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), "Full-grain leather boots with a block heel.", "Leather Ankle Boots", new Guid("00000000-0000-0000-0000-000000000012") });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000052"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "20% off all tops.", "Tops 20% Off" });

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000038"),
                column: "Name",
                value: "XS");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000039"),
                column: "Name",
                value: "S");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003a"),
                column: "Name",
                value: "M");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003b"),
                column: "Name",
                value: "L");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003c"),
                column: "Name",
                value: "XL");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003d"),
                column: "Name",
                value: "One Size");

            migrationBuilder.UpdateData(
                table: "Suppliers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                columns: new[] { "Email", "Name" },
                values: new object[] { "orders@atlastextiles.lk", "Atlas Textiles" });

            migrationBuilder.UpdateData(
                table: "Suppliers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                columns: new[] { "Email", "Name" },
                values: new object[] { "sales@nordicfootwear.lk", "Nordic Footwear" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Colours",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003f"));

            migrationBuilder.DeleteData(
                table: "Colours",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000040"));

            migrationBuilder.UpdateData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "Description",
                value: "Launch promotion for the new menu.");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "Name",
                value: "Pizza");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "Name",
                value: "Pasta");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "Name",
                value: "Beverages");

            migrationBuilder.UpdateData(
                table: "Categories",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "Name",
                value: "Desserts");

            migrationBuilder.UpdateData(
                table: "Collections",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Core menu items available every day.", "Classic Menu" });

            migrationBuilder.UpdateData(
                table: "Collections",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Featured meals and customer favourites.", "Signature Meals" });

            migrationBuilder.UpdateData(
                table: "Collections",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Beverages and sweet add-ons.", "Drinks and Desserts" });

            migrationBuilder.UpdateData(
                table: "Colours",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003e"),
                columns: new[] { "HexCode", "Name" },
                values: new object[] { null, "Default" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000031"),
                columns: new[] { "Name", "Price", "Sku" },
                values: new object[] { "Small / Default", 1200m, "PIZ-MARG-S" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000032"),
                columns: new[] { "ColourId", "Name", "Price", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000003e"), "Large / Default", 2200m, "PIZ-MARG-L" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000033"),
                columns: new[] { "ColourId", "Name", "Price", "SizeId", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000003e"), "Medium / Default", 1600m, new Guid("00000000-0000-0000-0000-000000000039"), "PIZ-PEP-M" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000034"),
                columns: new[] { "ColourId", "Name", "Price", "SizeId", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000003e"), "Large / Default", 2600m, new Guid("00000000-0000-0000-0000-00000000003a"), "PIZ-PEP-L" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000035"),
                columns: new[] { "Name", "Price", "SizeId", "Sku" },
                values: new object[] { "Regular / Default", 1800m, new Guid("00000000-0000-0000-0000-00000000003b"), "PST-CARB-R" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000036"),
                columns: new[] { "ColourId", "Name", "Price", "SizeId", "Sku" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000003e"), "330ml / Default", 300m, new Guid("00000000-0000-0000-0000-00000000003d"), "BEV-COLA-330" });

            migrationBuilder.UpdateData(
                table: "ProductVariants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000037"),
                columns: new[] { "Name", "Price", "SizeId", "Sku" },
                values: new object[] { "Single / Default", 900m, new Guid("00000000-0000-0000-0000-00000000003c"), "DES-TIRA-S" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Classic tomato, mozzarella and basil.", "Margherita Pizza" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"),
                columns: new[] { "CollectionId", "Description", "Name" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), "Pepperoni with mozzarella.", "Pepperoni Pizza" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Creamy pasta with pancetta.", "Spaghetti Carbonara" });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"),
                columns: new[] { "CollectionId", "Description", "Name", "SupplierId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), "Carbonated soft drink.", "Cola", new Guid("00000000-0000-0000-0000-000000000012") });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"),
                columns: new[] { "CollectionId", "Description", "Name", "SupplierId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), "Classic coffee dessert.", "Tiramisu", new Guid("00000000-0000-0000-0000-000000000011") });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000052"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "20% off all pizzas.", "Pizza 20% Off" });

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000038"),
                column: "Name",
                value: "Small");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000039"),
                column: "Name",
                value: "Medium");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003a"),
                column: "Name",
                value: "Large");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003b"),
                column: "Name",
                value: "Regular");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003c"),
                column: "Name",
                value: "Single");

            migrationBuilder.UpdateData(
                table: "Sizes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-00000000003d"),
                column: "Name",
                value: "330ml");

            migrationBuilder.UpdateData(
                table: "Suppliers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                columns: new[] { "Email", "Name" },
                values: new object[] { "orders@freshfoods.lk", "Fresh Foods Ltd" });

            migrationBuilder.UpdateData(
                table: "Suppliers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                columns: new[] { "Email", "Name" },
                values: new object[] { "sales@beverageco.lk", "Beverage Co" });
        }
    }
}
