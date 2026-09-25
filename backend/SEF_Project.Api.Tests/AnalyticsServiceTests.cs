using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Analytics;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Analytics;

namespace SEF_Project.Api.Tests;

public class AnalyticsServiceTests
{
    private static readonly DateTime Now = Utc(2026, 10, 15, 12);

    // Standard 10-day range; the previous window is [2026-09-21, 2026-10-01).
    private static readonly DateTime RangeFrom = Utc(2026, 10, 1);
    private static readonly DateTime RangeTo = Utc(2026, 10, 11);

    private static DateTime Utc(int year, int month, int day, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    }

    private sealed class TestDb : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public TestDb(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public AnalyticsService Service => new(Context, new FixedTimeProvider());

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private static async Task<TestDb> CreateDbAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);
        await context.Database.EnsureCreatedAsync();

        return new TestDb(connection, context);
    }

    private static async Task<int> SeedCustomerAsync(AppDbContext context)
    {
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "Test",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };
        var customer = new Customer { User = user };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        return customer.Id;
    }

    private static Order AddOrder(
        AppDbContext context,
        int customerId,
        OrderStatus status,
        DateTime placedAt,
        decimal discount,
        params (Guid VariantId, int Quantity, decimal UnitPrice)[] lines)
    {
        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}"[..20],
            CustomerId = customerId,
            Status = status,
            PlacedAt = placedAt,
            Currency = "LKR"
        };

        foreach (var (variantId, quantity, unitPrice) in lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductVariantId = variantId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * quantity
            });
        }

        order.Subtotal = order.Items.Sum(i => i.LineTotal);
        order.DiscountTotal = discount;
        order.Total = order.Subtotal - discount;

        context.Orders.Add(order);
        return order;
    }

    /// <summary>
    /// In range [10-01, 10-11): A (Confirmed) 2x Margherita S + 1x Cola = 2700;
    /// B (Completed) 1x Pepperoni L = 2600. Excluded: C Cancelled, D Pending,
    /// F Refunded. E (Completed, 09-25) is outside the range but inside the
    /// previous window.
    /// </summary>
    private static async Task<(int CustomerId, Order A)> SeedStandardOrdersAsync(AppDbContext context)
    {
        var customerId = await SeedCustomerAsync(context);

        var a = AddOrder(context, customerId, OrderStatus.Confirmed, Utc(2026, 10, 2, 10), 0m,
            (SeedData.VariantMargheritaSmall, 2, 1200m),
            (SeedData.VariantCola330, 1, 300m));
        AddOrder(context, customerId, OrderStatus.Completed, Utc(2026, 10, 5, 9), 0m,
            (SeedData.VariantPepperoniLarge, 1, 2600m));
        AddOrder(context, customerId, OrderStatus.Cancelled, Utc(2026, 10, 3), 0m,
            (SeedData.VariantTiramisuSingle, 5, 900m));
        AddOrder(context, customerId, OrderStatus.Pending, Utc(2026, 10, 4), 0m,
            (SeedData.VariantCarbonaraRegular, 1, 1800m));
        AddOrder(context, customerId, OrderStatus.Completed, Utc(2026, 9, 25), 0m,
            (SeedData.VariantMargheritaSmall, 4, 1200m));
        AddOrder(context, customerId, OrderStatus.Refunded, Utc(2026, 10, 6), 0m,
            (SeedData.VariantCola330, 3, 300m));

        await context.SaveChangesAsync();

        return (customerId, a);
    }

    // ---- Sales ---------------------------------------------------------------

    [Fact]
    public async Task GetSalesSummaryAsync_ShouldAggregateOnlySalesInRange()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        var summary = await db.Service.GetSalesSummaryAsync(
            new AnalyticsDateRangeQuery { From = RangeFrom, To = RangeTo });

        Assert.Equal(2, summary.OrderCount);
        Assert.Equal(4, summary.UnitsSold);
        Assert.Equal(5300m, summary.GrossSales);
        Assert.Equal(0m, summary.DiscountTotal);
        Assert.Equal(5300m, summary.NetRevenue);
        Assert.Equal(2650m, summary.AverageOrderValue);
        Assert.Equal("LKR", summary.Currency);
        Assert.DoesNotContain(OrderStatus.Cancelled, summary.IncludedStatuses);
        Assert.DoesNotContain(OrderStatus.Pending, summary.IncludedStatuses);
    }

    [Fact]
    public async Task GetSalesSummaryAsync_ShouldReturnZeros_WhenThereAreNoOrders()
    {
        await using var db = await CreateDbAsync();

        var summary = await db.Service.GetSalesSummaryAsync(
            new AnalyticsDateRangeQuery { From = RangeFrom, To = RangeTo });

        Assert.Equal(0, summary.OrderCount);
        Assert.Equal(0, summary.UnitsSold);
        Assert.Equal(0m, summary.NetRevenue);
        Assert.Equal(0m, summary.AverageOrderValue);
    }

    [Fact]
    public async Task GetSalesSummaryAsync_ShouldTreatToAsExclusive()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        // Order B was placed exactly at 2026-10-05 09:00.
        var excluded = await db.Service.GetSalesSummaryAsync(
            new AnalyticsDateRangeQuery { From = RangeFrom, To = Utc(2026, 10, 5, 9) });
        var included = await db.Service.GetSalesSummaryAsync(
            new AnalyticsDateRangeQuery { From = Utc(2026, 10, 5, 9), To = RangeTo });

        Assert.Equal(2700m, excluded.NetRevenue);
        Assert.Equal(2600m, included.NetRevenue);
    }

    [Fact]
    public async Task GetSalesSummaryAsync_ShouldDefaultToLast30Days()
    {
        await using var db = await CreateDbAsync();

        var summary = await db.Service.GetSalesSummaryAsync(new AnalyticsDateRangeQuery());

        Assert.Equal(Now, summary.To);
        Assert.Equal(Now.AddDays(-30), summary.From);
    }

    [Fact]
    public async Task GetSalesOverTimeAsync_ShouldReturnDailyPoints_IncludingEmptyDays()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        var result = await db.Service.GetSalesOverTimeAsync(new SalesOverTimeQuery
        {
            From = RangeFrom,
            To = RangeTo,
            Granularity = TimeGranularity.Day
        });

        Assert.Equal(10, result.Points.Count);
        Assert.Equal(RangeFrom, result.Points[0].PeriodStart);

        var october2 = result.Points.Single(p => p.PeriodStart == Utc(2026, 10, 2));
        Assert.Equal(1, october2.OrderCount);
        Assert.Equal(3, october2.UnitsSold);
        Assert.Equal(2700m, october2.NetRevenue);

        var october3 = result.Points.Single(p => p.PeriodStart == Utc(2026, 10, 3));
        Assert.Equal(0, october3.OrderCount);
        Assert.Equal(0m, october3.NetRevenue);

        Assert.Equal(5300m, result.Points.Sum(p => p.NetRevenue));
    }

    [Fact]
    public async Task GetSalesOverTimeAsync_ShouldGroupByMonth()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        var result = await db.Service.GetSalesOverTimeAsync(new SalesOverTimeQuery
        {
            From = Utc(2026, 9, 1),
            To = Utc(2026, 11, 1),
            Granularity = TimeGranularity.Month
        });

        Assert.Equal(2, result.Points.Count);
        Assert.Equal((1, 4, 4800m), (result.Points[0].OrderCount, result.Points[0].UnitsSold, result.Points[0].NetRevenue));
        Assert.Equal((2, 4, 5300m), (result.Points[1].OrderCount, result.Points[1].UnitsSold, result.Points[1].NetRevenue));
    }

    [Fact]
    public async Task GetSalesOverTimeAsync_ShouldReject_WhenDailyRangeIsTooLong()
    {
        await using var db = await CreateDbAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.Service.GetSalesOverTimeAsync(new SalesOverTimeQuery
            {
                From = Utc(2025, 1, 1),
                To = Utc(2026, 6, 1),
                Granularity = TimeGranularity.Day
            }));
    }

    [Theory]
    [InlineData(2026, 10, 11, 2026, 10, 1)]  // to before from
    [InlineData(2026, 10, 1, 2026, 10, 1)]   // empty range
    [InlineData(2022, 1, 1, 2026, 1, 1)]     // longer than 3 years
    public async Task GetSalesSummaryAsync_ShouldReject_WhenRangeIsInvalid(
        int fromYear, int fromMonth, int fromDay,
        int toYear, int toMonth, int toDay)
    {
        await using var db = await CreateDbAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            db.Service.GetSalesSummaryAsync(new AnalyticsDateRangeQuery
            {
                From = Utc(fromYear, fromMonth, fromDay),
                To = Utc(toYear, toMonth, toDay)
            }));
    }

    // ---- Product performance --------------------------------------------------

    [Fact]
    public async Task GetProductPerformanceAsync_ShouldRankTopSellersByUnits()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        var result = await db.Service.GetProductPerformanceAsync(new ProductPerformanceQuery
        {
            From = RangeFrom,
            To = RangeTo
        });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(
            new[] { "Margherita Pizza", "Cola", "Pepperoni Pizza", "Spaghetti Carbonara", "Tiramisu" },
            result.Items.Select(i => i.ProductName));

        var margherita = result.Items[0];
        Assert.Equal(2, margherita.UnitsSold);
        Assert.Equal(1, margherita.OrderCount);
        Assert.Equal(2400m, margherita.Revenue);
    }

    [Fact]
    public async Task GetProductPerformanceAsync_ShouldSortByRevenue()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        var result = await db.Service.GetProductPerformanceAsync(new ProductPerformanceQuery
        {
            From = RangeFrom,
            To = RangeTo,
            SortBy = "revenue"
        });

        Assert.Equal(
            new[] { 2600m, 2400m, 300m, 0m, 0m },
            result.Items.Select(i => i.Revenue));
    }

    [Fact]
    public async Task GetProductPerformanceAsync_ShouldListLowPerformersIncludingUnsold()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        var result = await db.Service.GetProductPerformanceAsync(new ProductPerformanceQuery
        {
            From = RangeFrom,
            To = RangeTo,
            SortDirection = "asc",
            PageSize = 2
        });

        // Cancelled Tiramisu and pending Carbonara orders do not count.
        Assert.Equal(new[] { "Spaghetti Carbonara", "Tiramisu" }, result.Items.Select(i => i.ProductName));
        Assert.All(result.Items, i => Assert.Equal(0, i.UnitsSold));
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task GetProductPerformanceAsync_ShouldPaginate()
    {
        await using var db = await CreateDbAsync();
        await SeedStandardOrdersAsync(db.Context);

        var result = await db.Service.GetProductPerformanceAsync(new ProductPerformanceQuery
        {
            From = RangeFrom,
            To = RangeTo,
            Page = 2,
            PageSize = 2
        });

        Assert.Equal(2, result.Page);
        Assert.Equal(new[] { "Pepperoni Pizza", "Spaghetti Carbonara" }, result.Items.Select(i => i.ProductName));
    }

    // ---- Inventory -----------------------------------------------------------

    private static async Task AdjustInventoryAsync(AppDbContext context)
    {
        var cola = await context.Inventory.SingleAsync(i => i.ProductVariantId == SeedData.VariantCola330);
        cola.ReservedQuantity = cola.QuantityOnHand; // available 0 -> out of stock

        var tiramisu = await context.Inventory.SingleAsync(i => i.ProductVariantId == SeedData.VariantTiramisuSingle);
        tiramisu.QuantityOnHand = 4; // reorder level 5 -> low stock

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetInventorySummaryAsync_ShouldCountStockStatuses()
    {
        await using var db = await CreateDbAsync();
        await AdjustInventoryAsync(db.Context);

        var summary = await db.Service.GetInventorySummaryAsync(includeInactive: false);

        Assert.Equal(7, summary.VariantCount);
        Assert.Equal(1, summary.OutOfStockCount);
        Assert.Equal(1, summary.LowStockCount);
        Assert.Equal(5, summary.InStockCount);
        Assert.Equal(50 + 30 + 40 + 25 + 35 + 200 + 4, summary.TotalQuantityOnHand);
        Assert.Equal(200, summary.TotalReservedQuantity);
    }

    [Fact]
    public async Task GetInventoryStockAsync_ShouldSortByAvailableQuantityAscending()
    {
        await using var db = await CreateDbAsync();
        await AdjustInventoryAsync(db.Context);

        var result = await db.Service.GetInventoryStockAsync(new InventoryStockQuery());

        Assert.Equal("BEV-COLA-330", result.Items[0].Sku);
        Assert.Equal(0, result.Items[0].AvailableQuantity);
        Assert.Equal(StockStatus.OutOfStock, result.Items[0].StockStatus);
        Assert.Equal("DES-TIRA-S", result.Items[1].Sku);
        Assert.Equal(StockStatus.LowStock, result.Items[1].StockStatus);
        Assert.Equal(StockStatus.InStock, result.Items[^1].StockStatus);
    }

    [Theory]
    [InlineData(StockStatus.LowStock, "DES-TIRA-S")]
    [InlineData(StockStatus.OutOfStock, "BEV-COLA-330")]
    public async Task GetInventoryStockAsync_ShouldFilterByStockStatus(
        StockStatus status,
        string expectedSku)
    {
        await using var db = await CreateDbAsync();
        await AdjustInventoryAsync(db.Context);

        var result = await db.Service.GetInventoryStockAsync(
            new InventoryStockQuery { StockStatus = status });

        Assert.Equal(expectedSku, Assert.Single(result.Items).Sku);
    }

    [Fact]
    public async Task GetInventoryStockAsync_ShouldTreatMissingInventoryRecordAsOutOfStock()
    {
        await using var db = await CreateDbAsync();

        db.Context.ProductVariants.Add(new ProductVariant
        {
            ProductId = SeedData.ProductCarbonara,
            Sku = "PST-CARB-L",
            Name = "Large",
            Price = 2400m,
            IsActive = true
        });
        await db.Context.SaveChangesAsync();

        var result = await db.Service.GetInventoryStockAsync(
            new InventoryStockQuery { Search = "carb-l" });

        var item = Assert.Single(result.Items);
        Assert.False(item.HasInventoryRecord);
        Assert.Equal(StockStatus.OutOfStock, item.StockStatus);
    }

    [Fact]
    public async Task GetInventoryStockAsync_ShouldExcludeInactiveVariants_ByDefault()
    {
        await using var db = await CreateDbAsync();

        var variant = await db.Context.ProductVariants
            .SingleAsync(v => v.Id == SeedData.VariantMargheritaLarge);
        variant.IsActive = false;
        await db.Context.SaveChangesAsync();

        var active = await db.Service.GetInventoryStockAsync(new InventoryStockQuery());
        var all = await db.Service.GetInventoryStockAsync(
            new InventoryStockQuery { IncludeInactive = true });

        Assert.Equal(6, active.TotalCount);
        Assert.Equal(7, all.TotalCount);
    }

    // ---- Promotion performance ----------------------------------------------

    [Fact]
    public async Task GetPromotionPerformanceAsync_ShouldMeasureCouponRedemptions()
    {
        await using var db = await CreateDbAsync();
        var customerId = await SeedCustomerAsync(db.Context);

        var redeemedOrder = AddOrder(db.Context, customerId, OrderStatus.Completed, Utc(2026, 10, 2), 480m,
            (SeedData.VariantMargheritaSmall, 2, 1200m));
        var cancelledOrder = AddOrder(db.Context, customerId, OrderStatus.Cancelled, Utc(2026, 10, 3), 0m,
            (SeedData.VariantPepperoniLarge, 1, 2600m));
        await db.Context.SaveChangesAsync();

        db.Context.CouponRedemptions.AddRange(
            new CouponRedemption
            {
                CouponId = SeedData.CouponSummer20,
                CustomerId = customerId,
                OrderId = redeemedOrder.Id,
                RedeemedAt = Utc(2026, 10, 2)
            },
            new CouponRedemption
            {
                CouponId = SeedData.CouponSummer20,
                CustomerId = customerId,
                OrderId = cancelledOrder.Id,
                RedeemedAt = Utc(2026, 10, 3)
            },
            new CouponRedemption
            {
                // Outside the date range.
                CouponId = SeedData.CouponFreeDelivery,
                CustomerId = customerId,
                RedeemedAt = Utc(2026, 9, 20)
            });
        await db.Context.SaveChangesAsync();

        var result = await db.Service.GetPromotionPerformanceAsync(new PromotionPerformanceQuery
        {
            From = RangeFrom,
            To = RangeTo
        });

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(3, result.LivePromotionCount);
        Assert.Equal(2, result.TotalRedemptions);

        var pizza = result.Items[0];
        Assert.Equal(SeedData.PromotionPizza20, pizza.PromotionId);
        Assert.True(pizza.IsLive);
        Assert.Equal(1, pizza.CouponCount);
        Assert.Equal(2, pizza.Redemptions);
        Assert.Equal(1, pizza.UniqueCustomers);
        Assert.Equal(1, pizza.RedeemedOrderCount);       // cancelled order excluded
        Assert.Equal(1920m, pizza.RedeemedOrderRevenue);  // 2400 - 480
        Assert.Equal(480m, pizza.DiscountAmount);

        var freeDelivery = result.Items.Single(i => i.PromotionId == SeedData.PromotionFreeDelivery);
        Assert.Equal(0, freeDelivery.Redemptions);

        var cola = result.Items.Single(i => i.PromotionId == SeedData.PromotionColaFixed);
        Assert.False(cola.IsLive);
    }

    [Fact]
    public async Task GetPromotionPerformanceAsync_ShouldReturnOnlyLive_WhenLiveOnly()
    {
        await using var db = await CreateDbAsync();

        var result = await db.Service.GetPromotionPerformanceAsync(new PromotionPerformanceQuery
        {
            From = RangeFrom,
            To = RangeTo,
            LiveOnly = true
        });

        Assert.Equal(3, result.TotalCount);
        Assert.All(result.Items, i => Assert.True(i.IsLive));
        Assert.All(result.Items, i => Assert.Equal(0, i.Redemptions));
    }

    // ---- Demand ----------------------------------------------------------------

    [Fact]
    public async Task GetDemandInsightsAsync_ShouldCalculateVelocityTrendAndCover()
    {
        await using var db = await CreateDbAsync();
        var (customerId, _) = await SeedStandardOrdersAsync(db.Context);

        // Cola: 1 unit in the previous window, 1 + 2 = 3 in the current one.
        AddOrder(db.Context, customerId, OrderStatus.Completed, Utc(2026, 9, 22), 0m,
            (SeedData.VariantCola330, 1, 300m));
        AddOrder(db.Context, customerId, OrderStatus.Confirmed, Utc(2026, 10, 8), 0m,
            (SeedData.VariantCola330, 2, 300m));
        await db.Context.SaveChangesAsync();

        var result = await db.Service.GetDemandInsightsAsync(new DemandQuery
        {
            From = RangeFrom,
            To = RangeTo
        });

        Assert.Equal(7, result.TotalCount);
        Assert.Equal(
            new[] { "BEV-COLA-330", "PIZ-MARG-S", "PIZ-PEP-L" },
            result.Items.Take(3).Select(i => i.Sku));

        var cola = result.Items[0];
        Assert.Equal(3, cola.UnitsSold);
        Assert.Equal(1, cola.PreviousUnitsSold);
        Assert.Equal(0.3m, cola.UnitsPerDay);
        Assert.Equal(200m, cola.TrendPercent);
        Assert.Equal(DemandTrend.Rising, cola.Trend);
        Assert.Equal(666.7m, cola.DaysOfCover); // 200 available / 0.3 per day

        var margherita = result.Items[1];
        Assert.Equal(2, margherita.UnitsSold);
        Assert.Equal(4, margherita.PreviousUnitsSold);
        Assert.Equal(-50m, margherita.TrendPercent);
        Assert.Equal(DemandTrend.Falling, margherita.Trend);
        Assert.Equal(250m, margherita.DaysOfCover); // 50 / 0.2

        var pepperoni = result.Items[2];
        Assert.Equal(DemandTrend.New, pepperoni.Trend);
        Assert.Null(pepperoni.TrendPercent);

        var unsold = result.Items.Single(i => i.Sku == "DES-TIRA-S");
        Assert.Equal(DemandTrend.NoSales, unsold.Trend);
        Assert.Equal(0m, unsold.UnitsPerDay);
        Assert.Null(unsold.DaysOfCover);
    }

    [Fact]
    public async Task GetDemandInsightsAsync_ShouldSortByTrend_WithNoBaselineLast()
    {
        await using var db = await CreateDbAsync();
        var (customerId, _) = await SeedStandardOrdersAsync(db.Context);

        AddOrder(db.Context, customerId, OrderStatus.Completed, Utc(2026, 9, 22), 0m,
            (SeedData.VariantCola330, 1, 300m));
        AddOrder(db.Context, customerId, OrderStatus.Confirmed, Utc(2026, 10, 8), 0m,
            (SeedData.VariantCola330, 2, 300m));
        await db.Context.SaveChangesAsync();

        var result = await db.Service.GetDemandInsightsAsync(new DemandQuery
        {
            From = RangeFrom,
            To = RangeTo,
            SortBy = "trend"
        });

        Assert.Equal(new[] { "BEV-COLA-330", "PIZ-MARG-S" }, result.Items.Take(2).Select(i => i.Sku));
        Assert.All(result.Items.Skip(2), i => Assert.Null(i.TrendPercent));
    }

    [Fact]
    public async Task GetDemandInsightsAsync_ShouldReturnNoSales_WhenDatasetIsEmpty()
    {
        await using var db = await CreateDbAsync();

        var result = await db.Service.GetDemandInsightsAsync(new DemandQuery
        {
            From = RangeFrom,
            To = RangeTo,
            SortDirection = "asc"
        });

        Assert.Equal(7, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal(DemandTrend.NoSales, i.Trend));
    }

    [Theory]
    [InlineData(0, 10, StockStatus.OutOfStock)]
    [InlineData(-3, 10, StockStatus.OutOfStock)]
    [InlineData(10, 10, StockStatus.LowStock)]
    [InlineData(1, 10, StockStatus.LowStock)]
    [InlineData(11, 10, StockStatus.InStock)]
    public void ClassifyStock_ShouldUseAvailableAgainstReorderLevel(
        int available,
        int reorderLevel,
        StockStatus expected)
    {
        Assert.Equal(expected, AnalyticsService.ClassifyStock(available, reorderLevel));
    }
}
