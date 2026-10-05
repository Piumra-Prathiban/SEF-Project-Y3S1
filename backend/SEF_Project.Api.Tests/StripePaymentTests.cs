using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Orders;
using SEF_Project.Api.Services.Payments;

namespace SEF_Project.Api.Tests;

public class StripePaymentTests
{
    private sealed class FakeStripePaymentService : IStripePaymentService
    {
        public StripePaymentIntentResult? Result { get; set; }

        public Task<StripePaymentIntentResult?> CreatePaymentIntentAsync(
            decimal amount,
            string currency,
            string orderNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result);

        public StripePaymentEvent? ParseWebhookEvent(
            string json,
            string signatureHeader) => null;
    }

    private static StripePaymentService CreateStripeService(StripeOptions options) =>
        new(Options.Create(options), NullLogger<StripePaymentService>.Instance);

    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task CreatePaymentIntent_ReturnsNull_WhenNotConfigured()
    {
        var service = CreateStripeService(new StripeOptions());

        var result = await service.CreatePaymentIntentAsync(100m, "LKR", "ORD-1");

        Assert.Null(result);
    }

    [Fact]
    public void ParseWebhookEvent_Throws_WhenWebhookSecretMissing()
    {
        var service = CreateStripeService(new StripeOptions
        {
            SecretKey = "sk_test_configured",
            WebhookSecret = string.Empty
        });

        Assert.Throws<InvalidOperationException>(
            () => service.ParseWebhookEvent("{}", "t=1,v1=bad"));
    }

    [Fact]
    public void ParseWebhookEvent_Throws_OnInvalidSignature()
    {
        var service = CreateStripeService(new StripeOptions
        {
            SecretKey = "sk_test_configured",
            WebhookSecret = "whsec_test"
        });

        Assert.Throws<Stripe.StripeException>(
            () => service.ParseWebhookEvent("{}", "t=1,v1=bad"));
    }

    [Fact]
    public async Task ApplyPaymentIntent_Success_CompletesPendingPayment()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = await CreateContextAsync(connection);
        var order = await SeedOrderWithPaymentAsync(context, "pi_test_123");

        var service = new OrderService(context, NullLogger<OrderService>.Instance);
        var applied = await service.ApplyPaymentIntentResultAsync("pi_test_123", true);

        Assert.True(applied);
        var payment = await context.Payments.SingleAsync(p => p.OrderId == order.Id);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.NotNull(payment.PaidAt);
    }

    [Fact]
    public async Task ApplyPaymentIntent_Failure_FailsPendingPayment()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = await CreateContextAsync(connection);
        var order = await SeedOrderWithPaymentAsync(context, "pi_test_456");

        var service = new OrderService(context, NullLogger<OrderService>.Instance);
        var applied = await service.ApplyPaymentIntentResultAsync("pi_test_456", false);

        Assert.True(applied);
        var payment = await context.Payments.SingleAsync(p => p.OrderId == order.Id);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Null(payment.PaidAt);
    }

    [Fact]
    public async Task ApplyPaymentIntent_IsIdempotent_ForSettledPayment()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = await CreateContextAsync(connection);
        var order = await SeedOrderWithPaymentAsync(context, "pi_test_789");
        var service = new OrderService(context, NullLogger<OrderService>.Instance);

        await service.ApplyPaymentIntentResultAsync("pi_test_789", true);
        var appliedAgain = await service.ApplyPaymentIntentResultAsync("pi_test_789", false);

        Assert.True(appliedAgain);
        var payment = await context.Payments.SingleAsync(p => p.OrderId == order.Id);
        Assert.Equal(PaymentStatus.Completed, payment.Status);
    }

    [Fact]
    public async Task ApplyPaymentIntent_ReturnsFalse_ForUnknownIntent()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = await CreateContextAsync(connection);
        await SeedOrderWithPaymentAsync(context, "pi_test_000");

        var service = new OrderService(context, NullLogger<OrderService>.Instance);
        var applied = await service.ApplyPaymentIntentResultAsync("pi_unknown", true);

        Assert.False(applied);
    }

    [Fact]
    public async Task CreateOrder_Card_ReturnsClientSecretAndReference()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = await CreateContextAsync(connection);
        var (user, _) = await SeedCustomerAsync(context);
        var fakeStripe = new FakeStripePaymentService
        {
            Result = new StripePaymentIntentResult("pi_test_123", "pi_test_123_secret_test")
        };
        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance,
            TimeProvider.System,
            fakeStripe);

        var response = await service.CreateOrderAsync(user.Id, BuildCreateOrderRequest(PaymentMethod.Card));

        var payment = Assert.Single(response.Payments);
        Assert.Equal("pi_test_123", payment.TransactionReference);
        Assert.Equal("pi_test_123_secret_test", payment.ClientSecret);
    }

    [Fact]
    public async Task CreateOrder_Cash_DoesNotCreatePaymentIntent()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = await CreateContextAsync(connection);
        var (user, _) = await SeedCustomerAsync(context);
        var fakeStripe = new FakeStripePaymentService
        {
            Result = new StripePaymentIntentResult("pi_test_123", "pi_test_123_secret_test")
        };
        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance,
            TimeProvider.System,
            fakeStripe);

        var response = await service.CreateOrderAsync(user.Id, BuildCreateOrderRequest(PaymentMethod.Cash));

        var payment = Assert.Single(response.Payments);
        Assert.Null(payment.TransactionReference);
        Assert.Null(payment.ClientSecret);
    }

    private static CreateOrderRequest BuildCreateOrderRequest(PaymentMethod method) =>
        new()
        {
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductVariantId = SeedData.VariantTShirtXs, Quantity = 1 }
            },
            DeliveryAddress = new CreateOrderAddressRequest
            {
                FullName = "Stripe Customer",
                Line1 = "1 Test Street",
                City = "Colombo",
                PostalCode = "00100"
            },
            PaymentMethod = method
        };

    private static async Task<(User User, Customer Customer)> SeedCustomerAsync(
        AppDbContext context)
    {
        var user = new User
        {
            Email = $"stripe-{Guid.NewGuid():N}@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "Stripe",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };
        var customer = new Customer { User = user };
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return (user, customer);
    }

    private static async Task<Order> SeedOrderWithPaymentAsync(
        AppDbContext context,
        string paymentIntentId)
    {
        var (_, customer) = await SeedCustomerAsync(context);

        var order = new Order
        {
            OrderNumber = "ORD-TEST-0001",
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            Total = 100m,
            PlacedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        context.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Amount = 100m,
            Method = PaymentMethod.Card,
            Status = PaymentStatus.Pending,
            TransactionReference = paymentIntentId
        });
        await context.SaveChangesAsync();

        return order;
    }
}
