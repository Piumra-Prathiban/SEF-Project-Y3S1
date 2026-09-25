using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEF_Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class FashionMarketingSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PromotionProducts",
                keyColumns: new[] { "ProductId", "PromotionId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000055") });

            migrationBuilder.InsertData(
                table: "PromotionProducts",
                columns: new[] { "ProductId", "PromotionId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000055") });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000055"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Rs. 50 off every pair of jeans.", "Denim Rs. 50 Off" });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000056"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "10% off every footwear style.", "Footwear Week 10% Off" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "PromotionProducts",
                keyColumns: new[] { "ProductId", "PromotionId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000023"), new Guid("00000000-0000-0000-0000-000000000055") });

            migrationBuilder.InsertData(
                table: "PromotionProducts",
                columns: new[] { "ProductId", "PromotionId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000055") });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000055"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "Rs. 50 off each cola.", "Cola Rs. 50 Off" });

            migrationBuilder.UpdateData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000056"),
                columns: new[] { "Description", "Name" },
                values: new object[] { "10% off every dessert.", "Dessert Week 10% Off" });
        }
    }
}
