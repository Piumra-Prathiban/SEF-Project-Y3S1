using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;

namespace SEF_Project.Api.Tests;

/// <summary>
/// API-level (real HTTP, real role checks) proof that a Staff reviewer -
/// authorized to use the endpoint at all, but not to approve a high-impact
/// proposal - is refused, and that an Administrator reviewing the exact same
/// workflow is allowed. Kept in its own test class with its own
/// <see cref="MarketingApiFactory"/> instance (not shared with
/// <see cref="InventoryPromotionAgentApiTests"/>) so the sales data this
/// test seeds cannot change what other tests in that class see.
/// </summary>
public class InventoryPromotionAgentApprovalAuthorizationTests : IClassFixture<MarketingApiFactory>
{
    private const string Url = "/api/agents/inventory-promotion/workflows";

    private readonly MarketingApiFactory _factory;

    public InventoryPromotionAgentApprovalAuthorizationTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedSharpDeclineAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var shopperUser = new User
        {
            Email = "authz-test-shopper@sef-project.test",
            PasswordHash = "not-used-in-this-test",
            FirstName = "Test",
            LastName = "Shopper",
            RoleId = 1,
            IsActive = true,
        };
        var shopper = new Customer { User = shopperUser };
        db.Customers.Add(shopper);
        await db.SaveChangesAsync();

        // 10 -> 2 units is an 80% decline, which the agent's discount policy
        // maps to a 20% discount: at (or above) HighImpactDiscountPercent,
        // making this proposal High impact and Administrator-only to approve.
        AddCompletedOrder(db, shopper.Id, SeedData.VariantCarbonaraRegular, 10, 1800m,
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        AddCompletedOrder(db, shopper.Id, SeedData.VariantCarbonaraRegular, 2, 1800m,
            new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();
    }

    private static void AddCompletedOrder(
        AppDbContext db, int customerId, Guid variantId, int quantity, decimal unitPrice, DateTime placedAt)
    {
        var order = new Order
        {
            OrderNumber = $"ORD-AUTHZ-{Guid.NewGuid():N}"[..20],
            CustomerId = customerId,
            Status = OrderStatus.Completed,
            PlacedAt = placedAt,
            Currency = "LKR",
            Subtotal = unitPrice * quantity,
            Total = unitPrice * quantity,
        };
        order.Items.Add(new OrderItem
        {
            ProductVariantId = variantId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = unitPrice * quantity,
        });
        db.Orders.Add(order);
    }

    [Fact]
    public async Task ApproveHighImpactProposal_ShouldRefuseStaff_ButAllowAdministrator()
    {
        await SeedSharpDeclineAsync();

        var staffClient = _factory.CreateClientAs("Staff");
        var started = await (await staffClient.PostAsJsonAsync(Url,
                new StartPromotionAgentRequest { Objective = "Find products with declining sales and recommend suitable promotions." }))
            .Content.ReadFromJsonAsync<PromotionAgentWorkflowResponse>();

        Assert.NotNull(started);
        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, started!.Status);
        Assert.Equal(PromotionImpactLevel.High, started.ImpactLevel);

        // Staff is authorized to use this endpoint at all (the class-level
        // [Authorize(Roles = "Staff,Administrator")] lets the request
        // through), but the business rule for *this* proposal still refuses
        // the decision -- a 409, not a 403, because the request itself was
        // legal; only this specific approval was not.
        var staffAttempt = await staffClient.PostAsJsonAsync(
            $"{Url}/{started.WorkflowId}/approve", new ReviewPromotionAgentRequest());
        Assert.Equal(HttpStatusCode.Conflict, staffAttempt.StatusCode);

        var problem = await staffAttempt.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Contains("Administrator", problem!["detail"].ToString());

        // The workflow is untouched: still awaiting approval, no promotion created.
        var stillPending = await staffClient.GetFromJsonAsync<PromotionAgentWorkflowResponse>(
            $"{Url}/{started.WorkflowId}");
        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, stillPending!.Status);
        Assert.Empty(stillPending.CreatedPromotionIds);

        // The same workflow, reviewed by an Administrator, succeeds.
        var adminAttempt = await _factory.CreateClientAs("Administrator")
            .PostAsJsonAsync($"{Url}/{started.WorkflowId}/approve", new ReviewPromotionAgentRequest());
        Assert.Equal(HttpStatusCode.OK, adminAttempt.StatusCode);

        var approved = await adminAttempt.Content.ReadFromJsonAsync<PromotionAgentWorkflowResponse>();
        Assert.Equal(AgentWorkflowStatus.Completed, approved!.Status);
        Assert.Single(approved.CreatedPromotionIds);
    }
}
