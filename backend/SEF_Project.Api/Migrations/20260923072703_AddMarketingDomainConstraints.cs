using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SEF_Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingDomainConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_DiscountValue",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_CouponRedemptions_CouponId",
                table: "CouponRedemptions");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Campaigns",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft");

            // Preserve existing campaign state before IsActive is dropped.
            migrationBuilder.Sql(
                "UPDATE \"Campaigns\" SET \"Status\" = CASE WHEN \"IsActive\" THEN 'Active' ELSE 'Paused' END;");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Campaigns");

            migrationBuilder.UpdateData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "Status",
                value: "Active");

            migrationBuilder.InsertData(
                table: "Campaigns",
                columns: new[] { "Id", "CreatedAt", "Description", "EndDate", "Name", "StartDate", "Status", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000054"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Weekend beverage deals.", new DateTime(2027, 3, 31, 0, 0, 0, 0, DateTimeKind.Utc), "Weekend Refresh", new DateTime(2027, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Scheduled", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.InsertData(
                table: "Promotions",
                columns: new[] { "Id", "CampaignId", "CreatedAt", "Description", "DiscountValue", "EndDate", "IsActive", "Name", "StartDate", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000056"), new Guid("00000000-0000-0000-0000-000000000051"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "10% off every dessert.", 10m, new DateTime(2026, 10, 31, 0, 0, 0, 0, DateTimeKind.Utc), true, "Dessert Week 10% Off", new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "PercentageDiscount", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("00000000-0000-0000-0000-000000000057"), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Free delivery with a coupon code.", 0m, new DateTime(2026, 12, 31, 0, 0, 0, 0, DateTimeKind.Utc), true, "Free Delivery", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "FreeShipping", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "Coupons",
                columns: new[] { "Id", "Code", "CreatedAt", "EndsAt", "IsActive", "PerCustomerLimit", "PromotionId", "StartsAt", "UpdatedAt", "UsageLimit" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000058"), "FREEDELIVERY", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2026, 12, 31, 0, 0, 0, 0, DateTimeKind.Utc), true, 3, new Guid("00000000-0000-0000-0000-000000000057"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 500 });

            migrationBuilder.InsertData(
                table: "PromotionCategories",
                columns: new[] { "CategoryId", "PromotionId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000056") });

            migrationBuilder.InsertData(
                table: "Promotions",
                columns: new[] { "Id", "CampaignId", "CreatedAt", "Description", "DiscountValue", "EndDate", "IsActive", "Name", "StartDate", "Type", "UpdatedAt" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000055"), new Guid("00000000-0000-0000-0000-000000000054"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Rs. 50 off each cola.", 50m, new DateTime(2027, 3, 31, 0, 0, 0, 0, DateTimeKind.Utc), true, "Cola Rs. 50 Off", new DateTime(2027, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "FixedAmountDiscount", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.InsertData(
                table: "PromotionProducts",
                columns: new[] { "ProductId", "PromotionId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000055") });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_DateRange",
                table: "Promotions",
                sql: "\"EndDate\" >= \"StartDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_DiscountValue",
                table: "Promotions",
                sql: "CAST(\"DiscountValue\" AS REAL) >= 0 AND (\"Type\" <> 'PercentageDiscount' OR (CAST(\"DiscountValue\" AS REAL) > 0 AND CAST(\"DiscountValue\" AS REAL) <= 100)) AND (\"Type\" <> 'FixedAmountDiscount' OR CAST(\"DiscountValue\" AS REAL) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Type",
                table: "Promotions",
                sql: "\"Type\" IN ('PercentageDiscount', 'FixedAmountDiscount', 'BuyXGetY', 'FreeShipping')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_DateRange",
                table: "Coupons",
                sql: "\"EndsAt\" >= \"StartsAt\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_PerCustomerLimit",
                table: "Coupons",
                sql: "\"PerCustomerLimit\" IS NULL OR \"PerCustomerLimit\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Coupons_UsageLimit",
                table: "Coupons",
                sql: "\"UsageLimit\" IS NULL OR \"UsageLimit\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_CouponId_OrderId",
                table: "CouponRedemptions",
                columns: new[] { "CouponId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_RedeemedAt",
                table: "CouponRedemptions",
                column: "RedeemedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_StartDate_EndDate",
                table: "Campaigns",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Status",
                table: "Campaigns",
                column: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Campaigns_DateRange",
                table: "Campaigns",
                sql: "\"EndDate\" >= \"StartDate\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Campaigns_Status",
                table: "Campaigns",
                sql: "\"Status\" IN ('Draft', 'Scheduled', 'Active', 'Paused', 'Completed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_DateRange",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_DiscountValue",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_Type",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_DateRange",
                table: "Coupons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_PerCustomerLimit",
                table: "Coupons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Coupons_UsageLimit",
                table: "Coupons");

            migrationBuilder.DropIndex(
                name: "IX_CouponRedemptions_CouponId_OrderId",
                table: "CouponRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_CouponRedemptions_RedeemedAt",
                table: "CouponRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_StartDate_EndDate",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_Status",
                table: "Campaigns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Campaigns_DateRange",
                table: "Campaigns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Campaigns_Status",
                table: "Campaigns");

            migrationBuilder.DeleteData(
                table: "Coupons",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000058"));

            migrationBuilder.DeleteData(
                table: "PromotionCategories",
                keyColumns: new[] { "CategoryId", "PromotionId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000056") });

            migrationBuilder.DeleteData(
                table: "PromotionProducts",
                keyColumns: new[] { "ProductId", "PromotionId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000024"), new Guid("00000000-0000-0000-0000-000000000055") });

            migrationBuilder.DeleteData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000055"));

            migrationBuilder.DeleteData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000056"));

            migrationBuilder.DeleteData(
                table: "Promotions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000057"));

            migrationBuilder.DeleteData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000054"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE \"Campaigns\" SET \"IsActive\" = (\"Status\" IN ('Active', 'Scheduled'));");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Campaigns");

            migrationBuilder.UpdateData(
                table: "Campaigns",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000051"),
                column: "IsActive",
                value: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_DiscountValue",
                table: "Promotions",
                sql: "\"DiscountValue\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_CouponId",
                table: "CouponRedemptions",
                column: "CouponId");
        }
    }
}
