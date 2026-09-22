using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.DTOs.Inventory;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Catalog;
using SEF_Project.Api.Services.InventoryManagement;

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
    public async Task AdjustStock_Receipt_ShouldIncreaseQuantityAndCreateLedgerRecord()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var catalogService = new CatalogService(context);
        var inventoryService = new InventoryService(context);

        var product = await catalogService.CreateProductAsync(new CreateProductRequest
        {
            Name = "Mushroom Soup"
        });

        var variant = await catalogService.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "SOP-MSH-1",
            Name = "Bowl",
            Price = 450m,
            InitialStock = 10,
            ReorderLevel = 5
        });

        var adjustment = await inventoryService.AdjustStockAsync(new StockAdjustmentRequest
        {
            ProductVariantId = variant.Id,
            Type = InventoryTransactionType.Receipt,
            QuantityChange = 25,
            Reference = "PO-9901",
            Note = "Restock shipment from vendor"
        });

        Assert.Equal(35, adjustment.QuantityOnHand);
        Assert.False(adjustment.IsLowStock);

        // Verify transaction ledger
        var transactions = await inventoryService.GetTransactionsAsync(variant.Id);
        Assert.Equal(2, transactions.Count); // Initial-stock + restock

        var latestTx = transactions.First();
        Assert.Equal("Receipt", latestTx.Type);
        Assert.Equal(25, latestTx.QuantityChange);
        Assert.Equal(35, latestTx.QuantityOnHandAfter);
        Assert.Equal("PO-9901", latestTx.Reference);
    }

    [Fact]
    public async Task AdjustStock_Sale_ShouldDecreaseStock()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var catalogService = new CatalogService(context);
        var inventoryService = new InventoryService(context);

        var product = await catalogService.CreateProductAsync(new CreateProductRequest { Name = "Iced Tea" });
        var variant = await catalogService.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "BEV-TEA-1",
            Name = "Glass",
            Price = 250m,
            InitialStock = 20,
            ReorderLevel = 5
        });

        var adjustment = await inventoryService.AdjustStockAsync(new StockAdjustmentRequest
        {
            ProductVariantId = variant.Id,
            Type = InventoryTransactionType.Sale,
            QuantityChange = -5,
            Reference = "OFFLINE-ORDER-1"
        });

        Assert.Equal(15, adjustment.QuantityOnHand);
    }

    [Fact]
    public async Task AdjustStock_ShouldThrow_WhenResultingQuantityIsNegative()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var catalogService = new CatalogService(context);
        var inventoryService = new InventoryService(context);

        var product = await catalogService.CreateProductAsync(new CreateProductRequest { Name = "Salad" });
        var variant = await catalogService.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "SLD-GRK-1",
            Name = "Greek Salad",
            Price = 700m,
            InitialStock = 5
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            inventoryService.AdjustStockAsync(new StockAdjustmentRequest
            {
                ProductVariantId = variant.Id,
                Type = InventoryTransactionType.Sale,
                QuantityChange = -10
            }));
    }

    [Fact]
    public async Task AdjustStock_ShouldThrow_WhenQuantityChangeIsZero()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var inventoryService = new InventoryService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            inventoryService.AdjustStockAsync(new StockAdjustmentRequest
            {
                ProductVariantId = Guid.NewGuid(),
                Type = InventoryTransactionType.Adjustment,
                QuantityChange = 0
            }));
    }

    [Fact]
    public async Task GetInventory_WithLowStockOnly_ShouldFilterAccurately()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var catalogService = new CatalogService(context);
        var inventoryService = new InventoryService(context);

        var product = await catalogService.CreateProductAsync(new CreateProductRequest { Name = "Desserts" });

        // Item 1: In stock (20 on hand, 5 reorder level)
        await catalogService.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "DES-CAKE-1",
            Name = "Chocolate Cake",
            Price = 500m,
            InitialStock = 20,
            ReorderLevel = 5
        });

        // Item 2: Low stock (2 on hand, 10 reorder level)
        await catalogService.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "DES-PUD-1",
            Name = "Caramel Pudding",
            Price = 400m,
            InitialStock = 2,
            ReorderLevel = 10
        });

        var lowStock = await inventoryService.GetInventoryAsync(new InventoryQuery
        {
            LowStockOnly = true
        });

        Assert.Contains(lowStock.Items, i => i.Sku == "DES-PUD-1" && i.IsLowStock);
        Assert.DoesNotContain(lowStock.Items, i => i.Sku == "DES-CAKE-1");
    }
}
