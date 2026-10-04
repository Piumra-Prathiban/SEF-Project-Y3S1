using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SEF_Project.Api.AI.FulfilmentException;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.Tests;

public class FulfilmentExceptionAgentTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void Controller_ShouldRequireStaffOrAdministrator()
    {
        var attribute = typeof(FulfilmentExceptionAgentController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Staff,Administrator", attribute.Roles);
    }

    [Fact]
    public void StartRequest_ShouldRequireAnObjective()
    {
        var request = new StartFulfilmentExceptionRequest { Objective = string.Empty, OrderId = Guid.NewGuid() };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StartFulfilmentExceptionRequest.Objective)));
    }

    [Fact]
    public async Task LocalModel_ShouldProposeFirstPermittedOrderTransition()
    {
        var orderId = Guid.NewGuid();
        var transitions = Transitions(
            ("order", orderId, "Pending", new[] { "Confirmed", "Cancelled" }),
            Array.Empty<(string, Guid, string, string[])>());

        var model = new LocalFulfilmentExceptionModel();
        var raw = await model.GenerateResolutionAsync(Context(orderId, transitions));

        var document = JsonSerializer.Deserialize<FulfilmentExceptionDocument>(raw, JsonOptions)!;

        Assert.Equal("Order", document.Resolution.TargetType);
        Assert.Equal("Confirmed", document.Resolution.Action);
        Assert.Equal("Pending", document.Resolution.Evidence.CurrentStatus);
    }

    [Fact]
    public async Task LocalModel_ShouldProposeNoActionWhenNothingIsPermitted()
    {
        var orderId = Guid.NewGuid();
        var transitions = Transitions(
            ("order", orderId, "Completed", Array.Empty<string>()),
            Array.Empty<(string, Guid, string, string[])>());

        var model = new LocalFulfilmentExceptionModel();
        var raw = await model.GenerateResolutionAsync(Context(orderId, transitions));

        var document = JsonSerializer.Deserialize<FulfilmentExceptionDocument>(raw, JsonOptions)!;

        Assert.Equal("None", document.Resolution.TargetType);
        Assert.Equal("NoAction", document.Resolution.Action);
    }

    [Fact]
    public void Validator_ShouldRejectAnOutOfStateAction()
    {
        var orderId = Guid.NewGuid();
        var transitions = Transitions(
            ("order", orderId, "Pending", new[] { "Confirmed", "Cancelled" }),
            Array.Empty<(string, Guid, string, string[])>());

        var document = Document("Order", orderId, "Completed", "Pending");

        var outcomes = FulfilmentExceptionValidator.Validate(document, transitions);

        Assert.Contains(outcomes, o => o.Rule == "TransitionAllowed" && !o.IsValid);
    }

    [Fact]
    public void Validator_ShouldRejectAnUnknownPaymentTarget()
    {
        var orderId = Guid.NewGuid();
        var transitions = Transitions(
            ("order", orderId, "Pending", new[] { "Confirmed" }),
            new[] { ("payment", Guid.NewGuid(), "Pending", new[] { "Completed" }) });

        var document = Document("Payment", Guid.NewGuid(), "Completed", "Pending");

        var outcomes = FulfilmentExceptionValidator.Validate(document, transitions);

        Assert.Contains(outcomes, o => o.Rule == "TargetExists" && !o.IsValid);
    }

    [Fact]
    public async Task StartAsync_ShouldProposeAResolutionAndAwaitApproval()
    {
        var (connection, context, orderId) = await CreateContextWithOrderAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var response = await service.StartAsync(new StartFulfilmentExceptionRequest
        {
            Objective = "Resolve this stuck order.",
            OrderId = orderId
        });

        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, response.Status);
        Assert.NotNull(response.Resolution);
        Assert.Equal("Order", response.Resolution!.Resolution.TargetType);
        Assert.Equal("Confirmed", response.Resolution.Resolution.Action);
    }

    [Fact]
    public async Task StartAsync_ShouldFailSafely_WhenOrderDoesNotExist()
    {
        var (connection, context, _) = await CreateContextWithOrderAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = CreateService(context);

        var response = await service.StartAsync(new StartFulfilmentExceptionRequest
        {
            Objective = "Resolve a missing order.",
            OrderId = Guid.NewGuid()
        });

        Assert.Equal(AgentWorkflowStatus.Failed, response.Status);
        Assert.NotEmpty(response.Errors);
    }

    [Fact]
    public async Task ApproveAsync_ShouldExecuteTheApprovedTransitionThroughOrderService()
    {
        var (connection, context, orderId) = await CreateContextWithOrderAsync();
        await using var _ = connection;
        await using var __ = context;

        var staffUserId = await SeedUserAsync(context, "fulfilment-staff@test.com", roleId: 2);
        var service = CreateService(context);

        var started = await service.StartAsync(new StartFulfilmentExceptionRequest
        {
            Objective = "Resolve this stuck order.",
            OrderId = orderId
        });

        var approved = await service.ApproveAsync(started.WorkflowId, staffUserId, "Looks correct.");

        Assert.NotNull(approved);
        Assert.Equal(AgentWorkflowStatus.Completed, approved!.Status);
        var order = await context.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    private static FulfilmentExceptionDocument Document(string targetType, Guid? targetId, string action, string currentStatus) =>
        new()
        {
            SchemaVersion = FulfilmentExceptionAgentConstants.SchemaVersion,
            Summary = "A test resolution.",
            Resolution = new FulfilmentResolution
            {
                TargetType = targetType,
                TargetId = targetId,
                Action = action,
                Rationale = "A rationale that is long enough.",
                Evidence = new FulfilmentResolutionEvidence { CurrentStatus = currentStatus }
            }
        };

    private static FulfilmentExceptionContext Context(Guid orderId, JsonNode transitions) =>
        new(
            "Resolve a stuck order.",
            orderId,
            null,
            JsonNode.Parse("{}")!,
            JsonNode.Parse("{}")!,
            JsonNode.Parse("{}")!,
            JsonNode.Parse("{}")!,
            transitions);

    private static JsonNode Transitions(
        (string, Guid, string, string[]) order,
        (string, Guid, string, string[])[] payments) =>
        JsonNode.Parse(JsonSerializer.Serialize(new
        {
            transitions = new
            {
                order = new
                {
                    orderId = order.Item2,
                    currentStatus = order.Item3,
                    allowed = order.Item4
                },
                payments = payments.Select(p => new
                {
                    paymentId = p.Item2,
                    currentStatus = p.Item3,
                    allowed = p.Item4
                }).ToList(),
                shipments = new List<object>()
            }
        }, JsonOptions))!;

    private static FulfilmentExceptionAgentService CreateService(AppDbContext context)
    {
        var tools = new IFulfilmentExceptionTool[]
        {
            new GetOrderDetailsTool(context),
            new GetPaymentsTool(context),
            new GetShipmentsTool(context),
            new GetStatusHistoryTool(context),
            new GetPermittedTransitionsTool(context)
        };
        var registry = new FulfilmentExceptionToolRegistry(
            tools,
            Options.Create(new FulfilmentExceptionAgentOptions()),
            TimeProvider.System,
            NullLogger<FulfilmentExceptionToolRegistry>.Instance);
        var orderService = new OrderService(
            context,
            NullLogger<OrderService>.Instance,
            TimeProvider.System);

        return new FulfilmentExceptionAgentService(
            context,
            registry,
            new LocalFulfilmentExceptionModel(),
            orderService,
            Options.Create(new FulfilmentExceptionAgentOptions()),
            TimeProvider.System,
            NullLogger<FulfilmentExceptionAgentService>.Instance);
    }

    private static async Task<(SqliteConnection, AppDbContext, Guid)> CreateContextWithOrderAsync()
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
            Email = $"fulfilment-{Guid.NewGuid():N}@test.com",
            PasswordHash = "not-a-real-hash",
            FirstName = "Fulfilment",
            LastName = "Customer",
            RoleId = 1,
            IsActive = true
        };
        context.Customers.Add(new Customer { User = user });
        await context.SaveChangesAsync();

        var order = await new OrderService(
            context,
            NullLogger<OrderService>.Instance,
            TimeProvider.System).CreateOrderAsync(user.Id, new CreateOrderRequest
            {
                Items = new List<CreateOrderItemRequest>
                {
                    new() { ProductVariantId = SeedData.VariantTShirtXs, Quantity = 1 }
                },
                DeliveryAddress = new CreateOrderAddressRequest
                {
                    FullName = "Fulfilment Customer",
                    Line1 = "1 Test Street",
                    City = "Colombo",
                    PostalCode = "00100"
                },
                PaymentMethod = PaymentMethod.Card
            });

        return (connection, context, order.Id);
    }

    private static async Task<int> SeedUserAsync(AppDbContext context, string email, int roleId)
    {
        var user = new User
        {
            Email = email,
            PasswordHash = "not-a-real-hash",
            FirstName = "Fulfilment",
            LastName = "Staff",
            RoleId = roleId,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }
}
