using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEF_Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class Member1ProductInventoryDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Colours",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HexCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colours", x => x.Id);
                    table.CheckConstraint("CK_Colours_HexCode_Format", "\"HexCode\" IS NULL OR \"HexCode\" ~ '^#[0-9A-Fa-f]{6}$'");
                });

            migrationBuilder.CreateTable(
                name: "Sizes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sizes", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Collections",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Core menu items available every day.", true, "Classic Menu", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Featured meals and customer favourites.", true, "Signature Meals", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Beverages and sweet add-ons.", true, "Drinks and Desserts", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "Colours",
                columns: new[] { "Id", "CreatedAt", "HexCode", "IsActive", "Name", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-00000000003e"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, "Default", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.InsertData(
                table: "Sizes",
                columns: new[] { "Id", "CreatedAt", "Description", "DisplayOrder", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000038"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 10, true, "Small", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-000000000039"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 20, true, "Medium", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-00000000003a"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 30, true, "Large", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-00000000003b"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 40, true, "Regular", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-00000000003c"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 50, true, "Single", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-00000000003d"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 60, true, "330ml", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollectionId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Products" AS p
                SET "CategoryId" = COALESCE(
                    (
                        SELECT pc."CategoryId"
                        FROM "ProductCategories" AS pc
                        WHERE pc."ProductId" = p."Id"
                        ORDER BY pc."CategoryId"
                        LIMIT 1
                    ),
                    '00000000-0000-0000-0000-000000000001'::uuid
                ),
                "CollectionId" = CASE
                    WHEN p."Id" = '00000000-0000-0000-0000-000000000022'::uuid THEN '00000000-0000-0000-0000-000000000006'::uuid
                    WHEN p."Id" IN (
                        '00000000-0000-0000-0000-000000000024'::uuid,
                        '00000000-0000-0000-0000-000000000025'::uuid
                    ) THEN '00000000-0000-0000-0000-000000000007'::uuid
                    ELSE '00000000-0000-0000-0000-000000000005'::uuid
                END
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "Products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CollectionId",
                table: "Products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ColourId",
                table: "ProductVariants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SizeId",
                table: "ProductVariants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "ProductVariants"
                SET "ColourId" = '00000000-0000-0000-0000-00000000003e'::uuid,
                    "SizeId" = CASE
                        WHEN "Name" = 'Small' OR "Sku" LIKE '%-S' THEN '00000000-0000-0000-0000-000000000038'::uuid
                        WHEN "Name" = 'Medium' OR "Sku" LIKE '%-M' THEN '00000000-0000-0000-0000-000000000039'::uuid
                        WHEN "Name" = 'Large' OR "Sku" LIKE '%-L' THEN '00000000-0000-0000-0000-00000000003a'::uuid
                        WHEN "Name" = 'Single' THEN '00000000-0000-0000-0000-00000000003c'::uuid
                        WHEN "Name" = '330ml' THEN '00000000-0000-0000-0000-00000000003d'::uuid
                        ELSE '00000000-0000-0000-0000-00000000003b'::uuid
                    END,
                    "Name" = CASE
                        WHEN "Name" LIKE '% / %' THEN "Name"
                        ELSE "Name" || ' / Default'
                    END
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ColourId",
                table: "ProductVariants",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SizeId",
                table: "ProductVariants",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_ProductVariants_ProductVariantId",
                table: "Inventory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Inventory",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_ProductVariantId",
                table: "Inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_QuantityOnHand",
                table: "Inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_ReorderLevel",
                table: "Inventory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Inventory_ReservedQuantity",
                table: "Inventory");

            migrationBuilder.RenameTable(
                name: "Inventory",
                newName: "InventoryStocks");

            migrationBuilder.AddPrimaryKey(
                name: "PK_InventoryStocks",
                table: "InventoryStocks",
                column: "Id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_AvailableQuantity",
                table: "InventoryStocks",
                sql: "\"QuantityOnHand\" >= \"ReservedQuantity\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_QuantityOnHand",
                table: "InventoryStocks",
                sql: "\"QuantityOnHand\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_ReorderLevel",
                table: "InventoryStocks",
                sql: "\"ReorderLevel\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStocks_ReservedQuantity",
                table: "InventoryStocks",
                sql: "\"ReservedQuantity\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStocks_ProductVariantId",
                table: "InventoryStocks",
                column: "ProductVariantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStocks_ProductVariants_ProductVariantId",
                table: "InventoryStocks",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryTransactions_ProductVariants_ProductVariantId",
                table: "InventoryTransactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_InventoryTransactions",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_CreatedAt",
                table: "InventoryTransactions");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_ProductVariantId",
                table: "InventoryTransactions");

            migrationBuilder.RenameTable(
                name: "InventoryTransactions",
                newName: "StockTransactions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockTransactions",
                table: "StockTransactions",
                column: "Id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockTransactions_QuantityChange",
                table: "StockTransactions",
                sql: "\"QuantityChange\" <> 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockTransactions_QuantityOnHandAfter",
                table: "StockTransactions",
                sql: "\"QuantityOnHandAfter\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_CreatedAt",
                table: "StockTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_ProductVariantId",
                table: "StockTransactions",
                column: "ProductVariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_ProductVariants_ProductVariantId",
                table: "StockTransactions",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "ProductCategories");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ColourId",
                table: "ProductVariants",
                column: "ColourId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_SizeId_ColourId",
                table: "ProductVariants",
                columns: new[] { "ProductId", "SizeId", "ColourId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_SizeId",
                table: "ProductVariants",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CollectionId",
                table: "Products",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_Name",
                table: "Collections",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colours_Name",
                table: "Colours",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_Name",
                table: "Sizes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Collections_CollectionId",
                table: "Products",
                column: "CollectionId",
                principalTable: "Collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Colours_ColourId",
                table: "ProductVariants",
                column: "ColourId",
                principalTable: "Colours",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Sizes_SizeId",
                table: "ProductVariants",
                column: "SizeId",
                principalTable: "Sizes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Products_Categories_CategoryId", table: "Products");
            migrationBuilder.DropForeignKey(name: "FK_Products_Collections_CollectionId", table: "Products");
            migrationBuilder.DropForeignKey(name: "FK_ProductVariants_Colours_ColourId", table: "ProductVariants");
            migrationBuilder.DropForeignKey(name: "FK_ProductVariants_Sizes_SizeId", table: "ProductVariants");
            migrationBuilder.DropForeignKey(name: "FK_InventoryStocks_ProductVariants_ProductVariantId", table: "InventoryStocks");
            migrationBuilder.DropForeignKey(name: "FK_StockTransactions_ProductVariants_ProductVariantId", table: "StockTransactions");

            migrationBuilder.CreateTable(
                name: "ProductCategories",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategories", x => new { x.ProductId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_ProductCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductCategories_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "ProductCategories" ("ProductId", "CategoryId")
                SELECT "Id", "CategoryId"
                FROM "Products"
                ON CONFLICT DO NOTHING
                """);

            migrationBuilder.DropIndex(name: "IX_ProductVariants_ColourId", table: "ProductVariants");
            migrationBuilder.DropIndex(name: "IX_ProductVariants_ProductId_SizeId_ColourId", table: "ProductVariants");
            migrationBuilder.DropIndex(name: "IX_ProductVariants_SizeId", table: "ProductVariants");
            migrationBuilder.DropIndex(name: "IX_Products_CategoryId", table: "Products");
            migrationBuilder.DropIndex(name: "IX_Products_CollectionId", table: "Products");
            migrationBuilder.DropIndex(name: "IX_InventoryStocks_ProductVariantId", table: "InventoryStocks");
            migrationBuilder.DropIndex(name: "IX_StockTransactions_CreatedAt", table: "StockTransactions");
            migrationBuilder.DropIndex(name: "IX_StockTransactions_ProductVariantId", table: "StockTransactions");

            migrationBuilder.DropCheckConstraint(name: "CK_InventoryStocks_AvailableQuantity", table: "InventoryStocks");
            migrationBuilder.DropCheckConstraint(name: "CK_InventoryStocks_QuantityOnHand", table: "InventoryStocks");
            migrationBuilder.DropCheckConstraint(name: "CK_InventoryStocks_ReorderLevel", table: "InventoryStocks");
            migrationBuilder.DropCheckConstraint(name: "CK_InventoryStocks_ReservedQuantity", table: "InventoryStocks");
            migrationBuilder.DropCheckConstraint(name: "CK_StockTransactions_QuantityChange", table: "StockTransactions");
            migrationBuilder.DropCheckConstraint(name: "CK_StockTransactions_QuantityOnHandAfter", table: "StockTransactions");

            migrationBuilder.DropPrimaryKey(name: "PK_InventoryStocks", table: "InventoryStocks");
            migrationBuilder.DropPrimaryKey(name: "PK_StockTransactions", table: "StockTransactions");

            migrationBuilder.RenameTable(name: "InventoryStocks", newName: "Inventory");
            migrationBuilder.RenameTable(name: "StockTransactions", newName: "InventoryTransactions");

            migrationBuilder.AddPrimaryKey(name: "PK_Inventory", table: "Inventory", column: "Id");
            migrationBuilder.AddPrimaryKey(name: "PK_InventoryTransactions", table: "InventoryTransactions", column: "Id");

            migrationBuilder.AddCheckConstraint(name: "CK_Inventory_QuantityOnHand", table: "Inventory", sql: "\"QuantityOnHand\" >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_Inventory_ReorderLevel", table: "Inventory", sql: "\"ReorderLevel\" >= 0");
            migrationBuilder.AddCheckConstraint(name: "CK_Inventory_ReservedQuantity", table: "Inventory", sql: "\"ReservedQuantity\" >= 0");

            migrationBuilder.CreateIndex(name: "IX_Inventory_ProductVariantId", table: "Inventory", column: "ProductVariantId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_InventoryTransactions_CreatedAt", table: "InventoryTransactions", column: "CreatedAt");
            migrationBuilder.CreateIndex(name: "IX_InventoryTransactions_ProductVariantId", table: "InventoryTransactions", column: "ProductVariantId");
            migrationBuilder.CreateIndex(name: "IX_ProductCategories_CategoryId", table: "ProductCategories", column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_ProductVariants_ProductVariantId",
                table: "Inventory",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryTransactions_ProductVariants_ProductVariantId",
                table: "InventoryTransactions",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                UPDATE "ProductVariants"
                SET "Name" = REPLACE("Name", ' / Default', '')
                """);

            migrationBuilder.DropColumn(name: "ColourId", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "SizeId", table: "ProductVariants");
            migrationBuilder.DropColumn(name: "CategoryId", table: "Products");
            migrationBuilder.DropColumn(name: "CollectionId", table: "Products");

            migrationBuilder.DropTable(name: "Collections");
            migrationBuilder.DropTable(name: "Colours");
            migrationBuilder.DropTable(name: "Sizes");
        }
    }
}
