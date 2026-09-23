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
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");

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
    public async Task AdjustStockAsync_ShouldReduceInventoryForSuccessfulStockOut()
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
            Type = InventoryTransactionType.StockOut,
            Quantity = 4,
            Reason = "Expired stock removal"
        });

        var transaction = await context.InventoryTransactions
            .AsNoTracking()
            .SingleAsync(t => t.ProductVariantId == inventory.ProductVariantId);

        Assert.NotNull(response);
        Assert.Equal(previousQuantity - 4, response.QuantityOnHand);
        Assert.Equal(InventoryTransactionType.StockOut, transaction.Type);
        Assert.Equal(-4, transaction.QuantityChange);
        Assert.Equal(previousQuantity, transaction.QuantityOnHandBefore);
        Assert.Equal(previousQuantity - 4, transaction.QuantityOnHandAfter);
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
    public async Task AdjustStockAsync_ShouldRollbackInventoryUpdate_WhenTransactionHistoryFails()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        var variantId = inventory.ProductVariantId;
        var previousQuantity = inventory.QuantityOnHand;
        var service = new InventoryService(context);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.AdjustStockAsync(
                new StockAdjustmentDto
                {
                    ProductVariantId = variantId,
                    Type = InventoryTransactionType.StockIn,
                    Quantity = 5,
                    Reason = "Rollback test"
                },
                performedByUserId: int.MaxValue));

        context.ChangeTracker.Clear();

        var reloadedInventory = await context.Inventory
            .AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == variantId);
        var transactions = await context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.ProductVariantId == variantId)
            .ToListAsync();

        Assert.Equal(previousQuantity, reloadedInventory.QuantityOnHand);
        Assert.Empty(transactions);
    }

    [Fact]
    public async Task GetStockTransactionsAsync_ShouldReturnCreatedHistory()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventory = await context.Inventory.FirstAsync();
        var previousQuantity = inventory.QuantityOnHand;
        var service = new InventoryService(context);

        await service.AdjustStockAsync(new StockAdjustmentDto
        {
            ProductVariantId = inventory.ProductVariantId,
            Type = InventoryTransactionType.StockIn,
            Quantity = 3,
            Reason = "History test"
        });

        var history = await service.GetStockTransactionsAsync(
            inventory.ProductVariantId);

        var transaction = Assert.Single(history);
        Assert.Equal(InventoryTransactionType.StockIn, transaction.Type);
        Assert.Equal(3, transaction.Quantity);
        Assert.Equal(3, transaction.QuantityChange);
        Assert.Equal(previousQuantity, transaction.PreviousQuantityOnHand);
        Assert.Equal(previousQuantity + 3, transaction.QuantityOnHandAfter);
        Assert.Equal("History test", transaction.Reason);
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
