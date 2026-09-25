using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEF_Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTransactionAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerformedByUserId",
                table: "StockTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityOnHandBefore",
                table: "StockTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                UPDATE "StockTransactions"
                SET "QuantityOnHandBefore" =
                    CASE
                        WHEN "Type" IN ('Reservation', 'ReservationRelease')
                            THEN "QuantityOnHandAfter"
                        ELSE GREATEST("QuantityOnHandAfter" - "QuantityChange", 0)
                    END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_PerformedByUserId",
                table: "StockTransactions",
                column: "PerformedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockTransactions_QuantityOnHandBefore",
                table: "StockTransactions",
                sql: "\"QuantityOnHandBefore\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_Users_PerformedByUserId",
                table: "StockTransactions",
                column: "PerformedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_Users_PerformedByUserId",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_PerformedByUserId",
                table: "StockTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockTransactions_QuantityOnHandBefore",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "PerformedByUserId",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "QuantityOnHandBefore",
                table: "StockTransactions");
        }
    }
}
