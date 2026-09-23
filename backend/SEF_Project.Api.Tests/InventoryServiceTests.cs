using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Tests;

public class InventoryServiceTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Context)>
        CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    [Fact]
    public async Task AdjustStockAsync_ShouldUpdateInventoryAndCreateTransaction()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        var previousQuantity = inventory.QuantityOnHand;
        var service = new InventoryService(context);

        var response = await service.AdjustStockAsync(new StockAdjustmentDto
        {
            ProductVariantId = inventory.ProductVariantId,
            Type = InventoryTransactionType.StockIn,
            Quantity = 7,
            Reason = "Supplier delivery",
            Reference = "GRN-001"
        });

        var transaction = await context.InventoryTransactions
            .AsNoTracking()
            .SingleAsync(t => t.ProductVariantId == inventory.ProductVariantId);

        Assert.NotNull(response);
        Assert.Equal(previousQuantity + 7, response.QuantityOnHand);
        Assert.Equal(InventoryTransactionType.StockIn, transaction.Type);
        Assert.Equal(7, transaction.QuantityChange);
        Assert.Equal(previousQuantity, transaction.QuantityOnHandBefore);
        Assert.Equal(previousQuantity + 7, transaction.QuantityOnHandAfter);
        Assert.Equal("Supplier delivery", transaction.Note);
        Assert.Equal("GRN-001", transaction.Reference);
    }

    [Fact]
    public async Task AdjustStockAsync_ShouldPreventNegativeStock()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        var service = new InventoryService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AdjustStockAsync(new StockAdjustmentDto
            {
                ProductVariantId = inventory.ProductVariantId,
                Type = InventoryTransactionType.StockOut,
                Quantity = inventory.QuantityOnHand + 1,
                Reason = "Damaged stock"
            }));

        Assert.Equal(
            "Stock adjustment cannot make quantity on hand negative.",
            exception.Message);

        Assert.Empty(await context.InventoryTransactions
            .Where(t => t.ProductVariantId == inventory.ProductVariantId)
            .ToListAsync());
    }

    [Fact]
    public async Task AdjustStockAsync_ShouldRejectInternalTransactionTypes()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        var service = new InventoryService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AdjustStockAsync(new StockAdjustmentDto
            {
                ProductVariantId = inventory.ProductVariantId,
                Type = InventoryTransactionType.Sale,
                Quantity = 1,
                Reason = "Manual sale correction"
            }));

        Assert.Equal(
            "Only StockIn, StockOut and Adjustment transactions can be created through manual stock adjustment.",
            exception.Message);
    }
}
