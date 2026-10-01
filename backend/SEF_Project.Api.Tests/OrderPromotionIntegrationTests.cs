using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Tests;

public class OrderPromotionIntegrationTests
{
    private static readonly DateTime DuringSummerCampaign =
        new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime utcNow)
        {
            _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }

    [Fact]
    public async Task CreateOrderAsync_AppliesBestLivePromotionAndPersistsGrossDiscountAndNetTotals()
    {
        var (connection, context, userId) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var order = await Service(context).CreateOrderAsync(
            userId,
            Request(couponCode: null));

        Assert.Equal(5000m, order.Subtotal);
        Assert.Equal(1000m, order.DiscountTotal);
        Assert.Equal(4000m, order.Total);
        var item = Assert.Single(order.Items);
        Assert.Equal(2000m, item.UnitPrice);
        Assert.Equal(4000m, item.LineTotal);
        Assert.Equal(4000m, Assert.Single(order.Payments).Amount);
    }

    [Fact]
    public async Task CreateOrderAsync_ValidatesCouponCaseInsensitivelyAndRecordsRedemption()
    {
        var (connection, context, userId) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var order = await Service(context).CreateOrderAsync(
            userId,
            Request("  summer20  "));

        Assert.Equal("SUMMER20", order.CouponCode);

        var redemption = await context.CouponRedemptions
            .AsNoTracking()
            .SingleAsync();
        Assert.Equal(SeedData.CouponSummer20, redemption.CouponId);
        Assert.Equal(order.Id, redemption.OrderId);
        Assert.Equal(DuringSummerCampaign, redemption.RedeemedAt);
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsAnUnknownCouponWithoutPersistingAnOrder()
    {
        var (connection, context, userId) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            Service(context).CreateOrderAsync(userId, Request("NOT-A-COUPON")));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await context.Orders.ToListAsync());
        Assert.Empty(await context.CouponRedemptions.ToListAsync());
    }

    [Fact]
    public async Task CreateOrderAsync_RejectsASecondRedemptionOnceThePerCustomerLimitIsReached()
    {
        var (connection, context, userId) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        // SUMMER20 has PerCustomerLimit = 1, so the same customer's second
        // attempt must be rejected even though the code itself is valid.
        await Service(context).CreateOrderAsync(userId, Request("SUMMER20"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Service(context).CreateOrderAsync(userId, Request("SUMMER20")));

        Assert.Contains("maximum number of times", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await context.Orders.ToListAsync());
        Assert.Single(await context.CouponRedemptions.ToListAsync());
    }

    [Fact]
    public async Task CreateOrderAsync_PersistsGrossDiscountNetTotalsAndTheRedeemedCouponToTheDatabase()
    {
        var (connection, context, userId) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var response = await Service(context).CreateOrderAsync(
            userId,
            Request("SUMMER20"));

        // Read back through a fresh, untracked query so this checks what was
        // actually written to the database, not just the in-memory response.
        var persisted = await context.Orders
            .AsNoTracking()
            .Include(o => o.CouponRedemptions)
                .ThenInclude(r => r.Coupon)
            .SingleAsync(o => o.Id == response.Id);

        Assert.Equal(5000m, persisted.Subtotal);
        Assert.Equal(1000m, persisted.DiscountTotal);
        Assert.Equal(4000m, persisted.Total);
        Assert.Equal("SUMMER20", persisted.CouponRedemptions.Single().Coupon.Code);
    }

    private static OrderService Service(AppDbContext context) =>
        new(
            context,
            NullLogger<OrderService>.Instance,
            new FixedTimeProvider(DuringSummerCampaign));

    private static CreateOrderRequest Request(string? couponCode) => new()
    {
        Items = new List<CreateOrderItemRequest>
        {
            new()
            {
                ProductVariantId = SeedData.VariantTShirtXs,
                Quantity = 2
            }
        },
        DeliveryAddress = new CreateOrderAddressRequest
        {
            FullName = "Promotion Customer",
            Line1 = "12 Main Street",
            City = "Colombo",
            PostalCode = "00100"
        },
        PaymentMethod = PaymentMethod.Card,
        CouponCode = couponCode
    };

    private static async Task<(SqliteConnection, AppDbContext, int)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
        await context.Database.EnsureCreatedAsync();

        var user = new User
        {
            Email = $"promotion-{Guid.NewGuid():N}@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "Promotion",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };
        context.Customers.Add(new Customer { User = user });
        await context.SaveChangesAsync();

        return (connection, context, user.Id);
    }
}
