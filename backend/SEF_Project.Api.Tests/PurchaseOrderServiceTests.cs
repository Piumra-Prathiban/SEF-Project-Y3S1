using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Tests;

public class PurchaseOrderServiceTests
{
    private static readonly Regex OrderNumberPattern =
        new(@"^PO-\d{4}-\d{4}$");

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

    private static async Task<int> SeedStaffUserAsync(AppDbContext context)
    {
        var user = new User
        {
            Email = "staff@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "Sam",
            LastName = "Staff",
            RoleId = 2,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user.Id;
    }

    private static PurchaseOrderService CreateService(AppDbContext context) =>
        new(context, NullLogger<PurchaseOrderService>.Instance);

    private static PurchaseOrderCreateRequest CreateRequest(
        Guid supplierId,
        params PurchaseOrderItemRequest[] items) =>
        new()
        {
            SupplierId = supplierId,
            ExpectedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Notes = "  Restock for the spring drop.  ",
            Items = items.ToList()
        };

    private static PurchaseOrderItemRequest Line(
        Guid variantId,
        int quantity,
        decimal unitCost) =>
        new()
        {
            ProductVariantId = variantId,
            Quantity = quantity,
            UnitCost = unitCost
        };

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldCreateDraftWithTotalsAndOrderNumber()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var response = await service.CreatePurchaseOrderAsync(
            CreateRequest(
                SeedData.SupplierAtlasTextiles,
                Line(SeedData.VariantTShirtXs, 5, 10.5m)));

        Assert.Equal(PurchaseOrderStatus.Draft, response.Status);
        Assert.Equal("Atlas Textiles", response.SupplierName);
        Assert.Matches(OrderNumberPattern, response.OrderNumber);
        Assert.Equal("Restock for the spring drop.", response.Notes);
        Assert.NotNull(response.ExpectedAt);
        Assert.Null(response.SubmittedAt);
        Assert.Null(response.ReceivedAt);

        var item = Assert.Single(response.Items);
        Assert.Equal(SeedData.VariantTShirtXs, item.ProductVariantId);
        Assert.Equal("TSH-CLS-XS", item.Sku);
        Assert.Equal("Classic Cotton T-Shirt", item.ProductName);
        Assert.Equal(5, item.Quantity);
        Assert.Equal(10.5m, item.UnitCost);
        Assert.Equal(52.5m, item.LineTotal);

        Assert.Equal(52.5m, response.Total);

        var stored = await context.PurchaseOrders
            .Include(o => o.Items)
            .SingleAsync();
        Assert.Equal(PurchaseOrderStatus.Draft, stored.Status);
        Assert.Single(stored.Items);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldGenerateUniqueSequentialOrderNumbers()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var first = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, 1m)));
        var second = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, 1m)));
        var third = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, 1m)));

        var numbers = new[] { first.OrderNumber, second.OrderNumber, third.OrderNumber };

        Assert.All(numbers, number => Assert.Matches(OrderNumberPattern, number));
        Assert.Equal(3, numbers.Distinct().Count());

        var suffixes = numbers
            .Select(number => int.Parse(number[(number.LastIndexOf('-') + 1)..]))
            .ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, suffixes);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectUnknownSupplier()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePurchaseOrderAsync(
                CreateRequest(Guid.NewGuid(), Line(SeedData.VariantTShirtXs, 1, 1m))));

        Assert.Equal("Supplier was not found.", exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectInactiveSupplier()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var supplier = new Supplier
        {
            Name = "Dormant Threads",
            IsActive = false
        };

        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePurchaseOrderAsync(
                CreateRequest(supplier.Id, Line(SeedData.VariantTShirtXs, 1, 1m))));

        Assert.Equal(
            "An inactive supplier cannot be used for a purchase order.",
            exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectUnknownVariant()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePurchaseOrderAsync(
                CreateRequest(SeedData.SupplierAtlasTextiles, Line(Guid.NewGuid(), 1, 1m))));

        Assert.Equal("Product variant was not found.", exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectNonPositiveQuantity()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePurchaseOrderAsync(
                CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 0, 1m))));

        Assert.Equal("Quantity must be greater than zero.", exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectNegativeUnitCost()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePurchaseOrderAsync(
                CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, -0.5m))));

        Assert.Equal("Unit cost cannot be negative.", exception.Message);
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectEmptyItems()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreatePurchaseOrderAsync(
                CreateRequest(SeedData.SupplierAtlasTextiles)));

        Assert.Equal(
            "A purchase order must contain at least one item.",
            exception.Message);
    }

    [Fact]
    public async Task SubmitPurchaseOrderAsync_ShouldMoveDraftToSubmittedWithTimestamp()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 2, 12m)));

        var submitted = await service.SubmitPurchaseOrderAsync(order.Id);

        Assert.NotNull(submitted);
        Assert.Equal(PurchaseOrderStatus.Submitted, submitted!.Status);
        Assert.NotNull(submitted.SubmittedAt);

        var stored = await context.PurchaseOrders.AsNoTracking().SingleAsync();
        Assert.Equal(PurchaseOrderStatus.Submitted, stored.Status);
        Assert.NotNull(stored.SubmittedAt);
    }

    [Fact]
    public async Task SubmitPurchaseOrderAsync_ShouldRejectSecondSubmit()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 2, 12m)));

        await service.SubmitPurchaseOrderAsync(order.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitPurchaseOrderAsync(order.Id));
    }

    [Fact]
    public async Task SubmitPurchaseOrderAsync_ShouldReturnNullWhenMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        Assert.Null(await service.SubmitPurchaseOrderAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_ShouldIncreaseStockAndWriteReceiptPerItem()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var staffUserId = await SeedStaffUserAsync(context);
        var service = CreateService(context);

        var xsBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantTShirtXs))
            .QuantityOnHand;
        var hoodieBefore = (await context.Inventory
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantHoodieM))
            .QuantityOnHand;

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(
                SeedData.SupplierAtlasTextiles,
                Line(SeedData.VariantTShirtXs, 4, 11m),
                Line(SeedData.VariantHoodieM, 6, 20m)));

        await service.SubmitPurchaseOrderAsync(order.Id);

        var received = await service.ReceivePurchaseOrderAsync(
            order.Id,
            staffUserId);

        Assert.NotNull(received);
        Assert.Equal(PurchaseOrderStatus.Received, received!.Status);
        Assert.NotNull(received.ReceivedAt);

        var xsAfter = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantTShirtXs);
        var hoodieAfter = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantHoodieM);

        Assert.Equal(xsBefore + 4, xsAfter.QuantityOnHand);
        Assert.Equal(hoodieBefore + 6, hoodieAfter.QuantityOnHand);

        var receipts = await context.InventoryTransactions.AsNoTracking()
            .Where(t => t.Type == InventoryTransactionType.Receipt)
            .ToListAsync();

        Assert.Equal(2, receipts.Count);

        var xsReceipt = receipts.Single(
            t => t.ProductVariantId == SeedData.VariantTShirtXs);
        Assert.Equal(4, xsReceipt.QuantityChange);
        Assert.Equal(xsBefore, xsReceipt.QuantityOnHandBefore);
        Assert.Equal(xsBefore + 4, xsReceipt.QuantityOnHandAfter);
        Assert.Equal(order.OrderNumber, xsReceipt.Reference);
        Assert.Equal(staffUserId, xsReceipt.PerformedByUserId);

        var orderAfter = await context.PurchaseOrders.AsNoTracking()
            .SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PurchaseOrderStatus.Received, orderAfter.Status);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_ShouldRejectReceivingTwice()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 3, 9m)));

        await service.SubmitPurchaseOrderAsync(order.Id);
        await service.ReceivePurchaseOrderAsync(order.Id);

        var onHandAfterFirstReceipt = (await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantTShirtXs))
            .QuantityOnHand;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReceivePurchaseOrderAsync(order.Id));

        var onHandAfterSecondAttempt = (await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantTShirtXs))
            .QuantityOnHand;

        Assert.Equal(onHandAfterFirstReceipt, onHandAfterSecondAttempt);

        var receiptCount = await context.InventoryTransactions.AsNoTracking()
            .CountAsync(t => t.Type == InventoryTransactionType.Receipt);
        Assert.Equal(1, receiptCount);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_ShouldRejectDraftOrder()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 3, 9m)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReceivePurchaseOrderAsync(order.Id));

        var receiptCount = await context.InventoryTransactions.AsNoTracking()
            .CountAsync(t => t.Type == InventoryTransactionType.Receipt);
        Assert.Equal(0, receiptCount);
    }

    [Fact]
    public async Task ReceivePurchaseOrderAsync_ShouldRollBackWhenALineCannotBeReceived()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(
                SeedData.SupplierAtlasTextiles,
                Line(SeedData.VariantTShirtXs, 5, 11m),
                Line(SeedData.VariantTShirtM, 2, 12m)));

        await service.SubmitPurchaseOrderAsync(order.Id);

        var xsBefore = (await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantTShirtXs))
            .QuantityOnHand;

        // Break the second line so the whole receipt must roll back.
        var missingInventory = await context.Inventory
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantTShirtM);
        context.Inventory.Remove(missingInventory);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReceivePurchaseOrderAsync(order.Id));

        var orderAfter = await context.PurchaseOrders.AsNoTracking()
            .SingleAsync(o => o.Id == order.Id);
        Assert.Equal(PurchaseOrderStatus.Submitted, orderAfter.Status);

        var xsAfter = await context.Inventory.AsNoTracking()
            .SingleAsync(i => i.ProductVariantId == SeedData.VariantTShirtXs);
        Assert.Equal(xsBefore, xsAfter.QuantityOnHand);

        var receiptCount = await context.InventoryTransactions.AsNoTracking()
            .CountAsync(t => t.Type == InventoryTransactionType.Receipt);
        Assert.Equal(0, receiptCount);
    }

    [Fact]
    public async Task CancelPurchaseOrderAsync_ShouldCancelDraftAndSubmitted()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var draft = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, 1m)));
        var submitted = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierNordicFootwear, Line(SeedData.VariantBootsOneSize, 1, 1m)));
        await service.SubmitPurchaseOrderAsync(submitted.Id);

        var cancelledDraft = await service.CancelPurchaseOrderAsync(draft.Id);
        var cancelledSubmitted = await service.CancelPurchaseOrderAsync(submitted.Id);

        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelledDraft!.Status);
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelledSubmitted!.Status);
    }

    [Fact]
    public async Task CancelPurchaseOrderAsync_ShouldRejectReceivedOrder()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, 1m)));

        await service.SubmitPurchaseOrderAsync(order.Id);
        await service.ReceivePurchaseOrderAsync(order.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPurchaseOrderAsync(order.Id));
    }

    [Fact]
    public async Task CancelPurchaseOrderAsync_ShouldRejectCancellingTwice()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var order = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, 1m)));

        await service.CancelPurchaseOrderAsync(order.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CancelPurchaseOrderAsync(order.Id));
    }

    [Fact]
    public async Task GetPurchaseOrdersAsync_ShouldPageAndFilterAndSortNewestFirst()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var atlasOne = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 2, 5m)));
        var atlasTwo = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantHoodieM, 1, 8m)));
        var nordic = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierNordicFootwear, Line(SeedData.VariantBootsOneSize, 1, 9m)));

        await service.SubmitPurchaseOrderAsync(nordic.Id);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await context.PurchaseOrders
            .Where(o => o.Id == atlasOne.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, baseTime));
        await context.PurchaseOrders
            .Where(o => o.Id == atlasTwo.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, baseTime.AddMinutes(1)));
        await context.PurchaseOrders
            .Where(o => o.Id == nordic.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, baseTime.AddMinutes(2)));

        var bySupplier = await service.GetPurchaseOrdersAsync(
            new PurchaseOrderQueryDto { SupplierId = SeedData.SupplierAtlasTextiles });

        Assert.Equal(2, bySupplier.TotalItems);
        Assert.Equal(2, bySupplier.Items.Count);

        var submittedOnly = await service.GetPurchaseOrdersAsync(
            new PurchaseOrderQueryDto { Status = PurchaseOrderStatus.Submitted });

        Assert.Equal(nordic.Id, Assert.Single(submittedOnly.Items).Id);

        var firstPage = await service.GetPurchaseOrdersAsync(
            new PurchaseOrderQueryDto { Page = 1, PageSize = 2 });

        Assert.Equal(3, firstPage.TotalItems);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.Equal(1, firstPage.Page);
        Assert.Equal(2, firstPage.PageSize);
        Assert.Equal(
            new[] { nordic.Id, atlasTwo.Id },
            firstPage.Items.Select(o => o.Id).ToArray());

        var secondPage = await service.GetPurchaseOrdersAsync(
            new PurchaseOrderQueryDto { Page = 2, PageSize = 2 });

        Assert.Equal(atlasOne.Id, Assert.Single(secondPage.Items).Id);
    }

    [Fact]
    public async Task GetPurchaseOrdersAsync_ShouldReturnLineTotalsAndGrandTotal()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        await service.CreatePurchaseOrderAsync(
            CreateRequest(
                SeedData.SupplierAtlasTextiles,
                Line(SeedData.VariantTShirtXs, 3, 10.25m),
                Line(SeedData.VariantHoodieM, 2, 20.5m)));

        var page = await service.GetPurchaseOrdersAsync(new PurchaseOrderQueryDto());

        var order = Assert.Single(page.Items);
        Assert.Equal(30.75m, order.Items
            .Single(i => i.ProductVariantId == SeedData.VariantTShirtXs)
            .LineTotal);
        Assert.Equal(41m, order.Items
            .Single(i => i.ProductVariantId == SeedData.VariantHoodieM)
            .LineTotal);
        Assert.Equal(71.75m, order.Total);
    }

    [Fact]
    public async Task GetPurchaseOrderByIdAsync_ShouldReturnOrderAndNullWhenMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var created = await service.CreatePurchaseOrderAsync(
            CreateRequest(SeedData.SupplierAtlasTextiles, Line(SeedData.VariantTShirtXs, 1, 7m)));

        var found = await service.GetPurchaseOrderByIdAsync(created.Id);
        var missing = await service.GetPurchaseOrderByIdAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal(created.OrderNumber, found!.OrderNumber);
        Assert.Null(missing);
    }
}
