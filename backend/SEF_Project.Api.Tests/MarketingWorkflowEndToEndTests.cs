using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.DTOs.Auth;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Auth;
using Xunit.Abstractions;

namespace SEF_Project.Api.Tests;

/// <summary>
/// Exercises the complete "declining sales -> promotion proposal -> approval
/// -> live promotion" scenario described in PHASE 11, end to end, over the
/// real HTTP pipeline (routing, JWT auth, role authorization, validation,
/// EF Core, the agent), exactly as React, Flutter and PostgreSQL would see
/// it. SQLite stands in for PostgreSQL for test speed and isolation only
/// (same EF Core provider-agnostic code path); see MARKETING_E2E_WORKFLOW.md
/// for the equivalent procedure against a real PostgreSQL database and a
/// browser/emulator.
/// </summary>
public class MarketingWorkflowEndToEndTests : IClassFixture<MarketingApiFactory>
{
    private const string AdminEmail = "marketing.admin@sef-project-e2e.test";
    private const string AdminPassword = "E2E-Test-Passw0rd!";
    private const string Objective = "Find products with declining sales and recommend suitable promotions.";

    private static readonly JsonSerializerOptions PrintOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly MarketingApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public MarketingWorkflowEndToEndTests(MarketingApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private void Print(string label, object value) =>
        _output.WriteLine($"--- {label} ---\n{JsonSerializer.Serialize(value, PrintOptions)}\n");

    /// <summary>Seeds the reviewer (a real password-hashed Administrator) and
    /// the customer whose declining orders the agent will analyse.</summary>
    private async Task<int> SeedScenarioDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var admin = new User
        {
            Email = AdminEmail,
            PasswordHash = passwordService.HashPassword(AdminPassword),
            FirstName = "Marketing",
            LastName = "Admin",
            RoleId = 3, // Administrator
            IsActive = true,
        };
        db.Users.Add(admin);

        var shopperUser = new User
        {
            Email = "regular.shopper@sef-project-e2e.test",
            PasswordHash = passwordService.HashPassword("not-used-in-this-test"),
            FirstName = "Regular",
            LastName = "Shopper",
            RoleId = 1, // Customer
            IsActive = true,
        };
        var shopper = new Customer { User = shopperUser };
        db.Customers.Add(shopper);

        await db.SaveChangesAsync();

        // Spaghetti Carbonara sold well in September, then fell sharply in
        // October: exactly the "declining demand" signal the objective asks
        // the agent to find. Same seeded product/variant and dates used
        // throughout this test suite, so this reproduces deterministically.
        AddCompletedOrder(db, shopper.Id, SeedData.VariantCarbonaraRegular, quantity: 4, unitPrice: 1800m,
            placedAt: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        AddCompletedOrder(db, shopper.Id, SeedData.VariantCarbonaraRegular, quantity: 3, unitPrice: 1800m,
            placedAt: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));

        await db.SaveChangesAsync();

        return admin.Id;
    }

    private static void AddCompletedOrder(
        AppDbContext db, int customerId, Guid variantId, int quantity, decimal unitPrice, DateTime placedAt)
    {
        var order = new Order
        {
            OrderNumber = $"ORD-E2E-{Guid.NewGuid():N}"[..20],
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
    public async Task DecliningSalesObjective_ShouldFlowFromReactRequest_ToApprovedCustomerFacingPromotion()
    {
        var adminUserId = await SeedScenarioDataAsync();

        // ---------------------------------------------------------------
        // Step 1-2. "React sends the marketing objective to ASP.NET Core."
        // ASP.NET Core authenticates (a real login over HTTP, not a
        // synthetic test token) and authorizes (Customers/anonymous users
        // are refused before any workflow is ever created).
        // ---------------------------------------------------------------
        using var anonymousClient = _factory.CreateClient();

        var unauthenticatedAttempt = await anonymousClient.PostAsJsonAsync(
            "/api/agents/inventory-promotion/workflows",
            new StartPromotionAgentRequest { Objective = Objective });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedAttempt.StatusCode);

        var customerAttempt = await _factory.CreateClientAs("Customer").PostAsJsonAsync(
            "/api/agents/inventory-promotion/workflows",
            new StartPromotionAgentRequest { Objective = Objective });
        Assert.Equal(HttpStatusCode.Forbidden, customerAttempt.StatusCode);

        var loginResponse = await anonymousClient.PostAsJsonAsync("/api/Auth/login",
            new LoginRequest { Email = AdminEmail, Password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var session = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(session);
        Assert.Equal("Administrator", session!.User.Role);
        Assert.Equal(adminUserId, session.User.Id);
        Print("Step 1-2: real login response", new { session.User, TokenIssued = !string.IsNullOrEmpty(session.Token) });

        using var reactClient = _factory.CreateClient();
        reactClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        // ---------------------------------------------------------------
        // Step 3-4. ASP.NET Core creates the AgentWorkflow row in the
        // database and the agent's planning stage records a structured plan
        // before any tool runs.
        // ---------------------------------------------------------------
        var startResponse = await reactClient.PostAsJsonAsync(
            "/api/agents/inventory-promotion/workflows",
            new StartPromotionAgentRequest { Objective = Objective, AnalysisDays = 30, MaxDiscountPercent = 30 });

        Assert.Equal(HttpStatusCode.Created, startResponse.StatusCode);
        Assert.NotNull(startResponse.Headers.Location);

        var afterStart = await startResponse.Content.ReadFromJsonAsync<PromotionAgentWorkflowResponse>();
        Assert.NotNull(afterStart);
        Assert.Equal(Objective, afterStart!.Objective);
        Assert.NotEmpty(afterStart.Plan);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.AgentWorkflows.SingleAsync(w => w.Id == afterStart.WorkflowId);
            Assert.Equal(Objective, stored.Objective);
            Assert.False(string.IsNullOrWhiteSpace(stored.PlanSummary));
        }

        // ---------------------------------------------------------------
        // Step 5-7. The Inventory & Promotion Agent executed its
        // controlled, read-only tools to retrieve sales velocity, inventory,
        // pricing and existing promotions -- nothing else.
        // ---------------------------------------------------------------
        // The gather step's own tools always run before drafting (steps are
        // ordered), but with a test host clock this coarse (a single frozen
        // instant, unlike TimeProvider.System's real resolution), calls
        // within that one step can tie on StartedAt, so their relative order
        // is not itself part of the contract -- only that all five ran.
        var gatherTools = afterStart.ToolExecutions
            .Select(t => t.ToolName)
            .Where(name => name != "SubmitPromotionProposal" && name != "CalculatePromotion")
            .ToHashSet();
        Assert.Equal(
            new HashSet<string> { "GetSalesVelocity", "GetActivePromotions", "GetInventory", "GetProductDetails", "GetProductPricing" },
            gatherTools);
        Assert.All(afterStart.ToolExecutions, t => Assert.Equal(AgentToolStatus.Success, t.Status));

        // ---------------------------------------------------------------
        // Step 8. The agent generated a structured proposal for the
        // declining product, and deterministic validation checked it.
        // ---------------------------------------------------------------
        var proposal = Assert.Single(afterStart.Proposal!.Proposals);
        Assert.Equal(SeedData.ProductCarbonara, proposal.ProductId);
        Assert.Equal("PercentageDiscount", proposal.PromotionType);
        Assert.Equal(15m, proposal.DiscountValue); // 4 -> 3 units is a 25% decline
        Assert.NotEmpty(afterStart.ValidationResults);
        Assert.All(afterStart.ValidationResults, v => Assert.True(v.IsValid, v.Message));

        // ---------------------------------------------------------------
        // Step 9. The workflow is now paused for a human reviewer (the
        // API's AwaitingApproval status -- the "PendingManagerApproval"
        // stage named in the scenario).
        // ---------------------------------------------------------------
        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, afterStart.Status);
        var pendingApproval = Assert.Single(afterStart.Approvals);
        Assert.Equal(ApprovalStatus.Pending, pendingApproval.Status);

        // ---------------------------------------------------------------
        // Step 10. React re-fetches the workflow to render the proposal and
        // the execution summary for the reviewer.
        // ---------------------------------------------------------------
        var fetchedForReview = await reactClient.GetFromJsonAsync<PromotionAgentWorkflowResponse>(
            $"/api/agents/inventory-promotion/workflows/{afterStart.WorkflowId}");
        Assert.NotNull(fetchedForReview);
        Assert.Equal(afterStart.WorkflowId, fetchedForReview!.WorkflowId);
        Print("Step 9-10: workflow awaiting approval (as React would display it)", fetchedForReview);

        // ---------------------------------------------------------------
        // Step 11-12. The Administrator approves; ASP.NET Core validates
        // that the decision is legal for this workflow's current state.
        // ---------------------------------------------------------------
        var approveResponse = await reactClient.PostAsJsonAsync(
            $"/api/agents/inventory-promotion/workflows/{afterStart.WorkflowId}/approve",
            new ReviewPromotionAgentRequest { Comment = "Approved for the autumn promotion push." });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var afterApproval = await approveResponse.Content.ReadFromJsonAsync<PromotionAgentWorkflowResponse>();
        Assert.NotNull(afterApproval);

        // A duplicate approval attempt is rejected (no second execution).
        var duplicateApproval = await reactClient.PostAsJsonAsync(
            $"/api/agents/inventory-promotion/workflows/{afterStart.WorkflowId}/approve",
            new ReviewPromotionAgentRequest());
        Assert.Equal(HttpStatusCode.Conflict, duplicateApproval.StatusCode);

        // ---------------------------------------------------------------
        // Step 13-14. The approved action executed through the normal,
        // authoritative PromotionService (never a raw database write from
        // the agent), and PostgreSQL now holds the promotion and the
        // completed workflow's recorded state and approval decision.
        // ---------------------------------------------------------------
        Assert.Equal(AgentWorkflowStatus.Completed, afterApproval!.Status);
        var createdPromotionId = Assert.Single(afterApproval.CreatedPromotionIds);
        Assert.Contains("Created 1 promotion", afterApproval.FinalOutcome);

        var decision = Assert.Single(afterApproval.Approvals);
        Assert.Equal(ApprovalStatus.Approved, decision.Status);
        Assert.Equal(adminUserId, decision.ReviewedByUserId);
        Assert.NotNull(decision.ReviewedAt);
        Assert.Equal("Approved for the autumn promotion push.", decision.Comment);
        Assert.Empty(afterApproval.Errors);
        Print("Step 13-14: completed workflow with the recorded approval decision", afterApproval);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var storedPromotion = await db.Promotions
                .Include(p => p.PromotionProducts)
                .SingleAsync(p => p.Id == createdPromotionId);

            Assert.True(storedPromotion.IsActive);
            Assert.Equal(15m, storedPromotion.DiscountValue);
            Assert.Equal(SeedData.ProductCarbonara, Assert.Single(storedPromotion.PromotionProducts).ProductId);

            var storedWorkflow = await db.AgentWorkflows.SingleAsync(w => w.Id == afterStart.WorkflowId);
            Assert.Equal(AgentWorkflowStatus.Completed, storedWorkflow.Status);
        }

        // ---------------------------------------------------------------
        // Step 15-16. Flutter (and any other customer client) retrieves the
        // now-active promotion through the same public, unauthenticated
        // endpoint used throughout the mobile app -- no login, no direct
        // database access, no direct call into the agent.
        //
        // The agent deliberately schedules a new promotion to start the day
        // after approval (never mid-instant "right now"), so its window
        // opens once that day actually arrives; the shared test clock is
        // advanced to that moment, exactly as a real deployment reaches it
        // by the calendar turning over, before checking what a customer sees.
        // ---------------------------------------------------------------
        _factory.AdvanceClockTo(proposal.StartDate);

        using var customerFacingClient = _factory.CreateClient(); // no Authorization header at all

        var promotionOffer = await customerFacingClient.GetFromJsonAsync<PromotionProductsResponse>(
            $"/api/promotions/{createdPromotionId}/products");
        Assert.NotNull(promotionOffer);
        Assert.True(promotionOffer!.HasPriceDiscount);
        var offeredVariant = Assert.Single(Assert.Single(promotionOffer.Products).Variants);
        Assert.Equal(1800m, offeredVariant.OriginalPrice);
        Assert.Equal(270m, offeredVariant.DiscountAmount);
        Assert.Equal(1530m, offeredVariant.FinalPrice);

        var productView = await customerFacingClient.GetFromJsonAsync<ProductPromotionsResponse>(
            $"/api/promotions/products/{SeedData.ProductCarbonara}");
        Assert.NotNull(productView);
        Assert.True(productView!.HasActivePromotion);
        Assert.Contains(productView.Promotions, p => p.Id == createdPromotionId);
        var customerVisiblePrice = Assert.Single(productView.Variants);
        Assert.Equal(createdPromotionId, customerVisiblePrice.PromotionId);
        Assert.Equal(1530m, customerVisiblePrice.FinalPrice);
        Print("Step 15-16: what a customer (Flutter/React) now sees for this product", productView);
    }
}
