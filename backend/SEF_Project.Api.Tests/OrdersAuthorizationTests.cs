using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Tests;

public class OrdersAuthorizationTests
{
    [Fact]
    public void OrdersController_ShouldRequireAuthentication()
    {
        var attribute = typeof(OrdersController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
    }

    [Theory]
    [InlineData(nameof(OrdersController.UpdateOrderStatus))]
    [InlineData(nameof(OrdersController.UpdatePaymentStatus))]
    [InlineData(nameof(OrdersController.CreateShipment))]
    [InlineData(nameof(OrdersController.UpdateShipmentStatus))]
    public void StaffOperations_ShouldRequireStaffRoles(string methodName)
    {
        var method = typeof(OrdersController).GetMethod(methodName);

        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Staff,Administrator", attribute!.Roles);
    }

    [Theory]
    [InlineData(nameof(OrdersController.CreateOrder))]
    [InlineData(nameof(OrdersController.GetOrders))]
    [InlineData(nameof(OrdersController.GetOrderById))]
    [InlineData(nameof(OrdersController.GetOrderStatusHistory))]
    [InlineData(nameof(OrdersController.CreatePayment))]
    [InlineData(nameof(OrdersController.GetPayments))]
    [InlineData(nameof(OrdersController.GetShipments))]
    [InlineData(nameof(OrdersController.CancelOrder))]
    public void CustomerOperations_ShouldNotRequireSpecificRoles(
        string methodName)
    {
        var method = typeof(OrdersController).GetMethod(methodName);

        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.True(attribute is null || attribute.Roles is null);
    }

    [Fact]
    public void CreateOrderItemRequest_ShouldNotExposePriceFields()
    {
        var properties = typeof(CreateOrderItemRequest).GetProperties();

        Assert.DoesNotContain(
            properties,
            p => p.Name is "Price" or "UnitPrice" or "LineTotal");
        Assert.Contains(
            properties,
            p => p.Name == nameof(CreateOrderItemRequest.ProductVariantId));
        Assert.Contains(
            properties,
            p => p.Name == nameof(CreateOrderItemRequest.Quantity));
    }

    [Fact]
    public void PaymentModels_ShouldNotExposeSensitiveFields()
    {
        var paymentProperties = typeof(Payment).GetProperties();
        var responseProperties = typeof(PaymentResponse).GetProperties();

        var forbiddenNames = new[]
        {
            "CardNumber",
            "Card",
            "Cvv",
            "Cvv2",
            "Password",
            "Pin",
            "Secret",
            "Token"
        };

        foreach (var name in forbiddenNames)
        {
            Assert.DoesNotContain(
                paymentProperties,
                p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                responseProperties,
                p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task GetOrdersAsync_CustomerCannotUseCustomerIdFilter()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var (userIdA, customerIdA) =
            await SeedCustomerAsync(context, "a@test.com");
        var (userIdB, customerIdB) =
            await SeedCustomerAsync(context, "b@test.com");

        await SeedOrderAsync(context, customerIdA, 100m);
        await SeedOrderAsync(context, customerIdB, 200m);

        var service = new OrderService(
            context,
            NullLogger<OrderService>.Instance);

        var result = await service.GetOrdersAsync(
            userIdA,
            false,
            new OrderQuery { CustomerId = customerIdB, PageSize = 50 });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(100m, Assert.Single(result.Items).Total);
    }

    private static async Task<(int UserId, int CustomerId)> SeedCustomerAsync(
        AppDbContext context,
        string email)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = "not-a-real-hash",
            FirstName = "Test",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };

        context.Users.Add(user);
        context.Customers.Add(new Customer { User = user });

        await context.SaveChangesAsync();

        var customer = await context.Customers
            .SingleAsync(c => c.UserId == user.Id);

        return (user.Id, customer.Id);
    }

    private static async Task SeedOrderAsync(
        AppDbContext context,
        int customerId,
        decimal total)
    {
        context.Orders.Add(new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}",
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            Subtotal = total,
            Total = total,
            PlacedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}
