using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SEF_Project.Api.AI.InventoryPromotion;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Analytics;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Tests;

public class InventoryPromotionAgentTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Tomorrow = new(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc);

    private const string Objective = "Find products with declining sales and recommend suitable promotions.";

    // ---- Harness -------------------------------------------------------------------

    /// <summary>
    /// Starts at <see cref="Now"/> and advances 1 ms per read, like a real
    /// clock, so audit records have a stable order.
    /// </summary>
    private sealed class FixedTimeProvider : TimeProvider
    {
        private long _ticks = new DateTimeOffset(Now, TimeSpan.Zero).UtcTicks;

        public override DateTimeOffset GetUtcNow() =>
            new(Interlocked.Add(ref _ticks, TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
    }

    private sealed class FakeModel : IPromotionProposalModel
    {
        private readonly Func<PromotionAgentContext, CancellationToken, Task<string>> _generate;

        public FakeModel(Func<PromotionAgentContext, CancellationToken, Task<string>> generate) => _generate = generate;

        public static FakeModel Returning(string output) => new((_, _) => Task.FromResult(output));

        public string Name => "FakeModel";

        public Task<string> GenerateProposalAsync(PromotionAgentContext context, CancellationToken cancellationToken) =>
            _generate(context, cancellationToken);
    }

    private sealed class FakeTool : IPromotionAgentTool
    {
        private readonly Func<CancellationToken, Task<object>> _execute;

        public FakeTool(string name, Func<CancellationToken, Task<object>> execute)
        {
            Name = name;
            _execute = execute;
        }

        public string Name { get; }

        public string RequiredOutputProperty => "items";

        public int Calls { get; private set; }

        public Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
        {
            Calls++;
            return _execute(cancellationToken);
        }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private Harness(SqliteConnection connection, AppDbContext context)
        {
            _connection = connection;
            Context = context;
        }

        public AppDbContext Context { get; }

        public int CustomerId { get; private set; }

        public int StaffUserId { get; private set; }

        public int AdminUserId { get; private set; }

        public InventoryPromotionAgentOptions AgentOptions { get; } = new()
        {
            ToolTimeout = TimeSpan.FromSeconds(5),
            ModelTimeout = TimeSpan.FromSeconds(5),
            MaxToolRetries = 2
        };

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();

            var harness = new Harness(connection, context);
            await harness.SeedUsersAsync();
            return harness;
        }

        private async Task SeedUsersAsync()
        {
            User NewUser(string email, int roleId) => new()
            {
                Email = email,
                PasswordHash = "not-a-real-hash",
                FirstName = "Test",
                LastName = email,
                RoleId = roleId,
                IsActive = true
            };

            var customerUser = NewUser("customer@test.com", 1);
            var staff = NewUser("staff@test.com", 2);
            var admin = NewUser("admin@test.com", 3);
            var customer = new Customer { User = customerUser };

            Context.Users.AddRange(staff, admin);
            Context.Customers.Add(customer);
            await Context.SaveChangesAsync();

            CustomerId = customer.Id;
            StaffUserId = staff.Id;
            AdminUserId = admin.Id;
        }

        public async Task AddSaleAsync(Guid variantId, int quantity, decimal unitPrice, DateTime placedAt)
        {
            var order = new Order
            {
                OrderNumber = $"ORD-{Guid.NewGuid():N}"[..20],
                CustomerId = CustomerId,
                Status = OrderStatus.Completed,
                PlacedAt = placedAt,
                Currency = "LKR",
                Subtotal = unitPrice * quantity,
                Total = unitPrice * quantity
            };
            order.Items.Add(new OrderItem
            {
                ProductVariantId = variantId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineTotal = unitPrice * quantity
            });

            Context.Orders.Add(order);
            await Context.SaveChangesAsync();
        }

        /// <summary>
        /// Carbonara declines (previous window vs last 30 days). Margherita
        /// declines too but already has the live "Pizza 20% Off" promotion.
        /// </summary>
        public async Task SeedDecliningSalesAsync(int carbonaraPrevious, int carbonaraCurrent)
        {
            await AddSaleAsync(SeedData.VariantCarbonaraRegular, carbonaraPrevious, 1800m, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
            await AddSaleAsync(SeedData.VariantCarbonaraRegular, carbonaraCurrent, 1800m, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
            await AddSaleAsync(SeedData.VariantMargheritaSmall, 10, 1200m, new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
            await AddSaleAsync(SeedData.VariantMargheritaSmall, 2, 1200m, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));
        }

        public TimeProvider Clock { get; } = new FixedTimeProvider();

        public PromotionService PromotionService() =>
            new(Context, Clock, NullLogger<PromotionService>.Instance);

        public PromotionAgentToolRegistry Registry(params IPromotionAgentTool[] overrides)
        {
            var time = Clock;
            var analytics = new AnalyticsService(Context, time);
            var offers = new PromotionOfferService(Context, time);

            var tools = new List<IPromotionAgentTool>
            {
                new GetSalesVelocityTool(analytics, time),
                new GetInventoryTool(analytics),
                new GetActivePromotionsTool(PromotionService()),
                new GetProductDetailsTool(offers),
                new GetProductPricingTool(offers),
                new CalculatePromotionTool(offers)
            };

            foreach (var tool in overrides)
            {
                tools.RemoveAll(t => t.Name == tool.Name);
                tools.Add(tool);
            }

            return new PromotionAgentToolRegistry(tools, Options.Create(AgentOptions), Clock,
                NullLogger<PromotionAgentToolRegistry>.Instance);
        }

        public InventoryPromotionAgentService Agent(IPromotionProposalModel? model = null, params IPromotionAgentTool[] overrides) =>
            new(Context, Registry(overrides), model ?? new LocalPromotionProposalModel(), PromotionService(),
                Options.Create(AgentOptions), Clock, NullLogger<InventoryPromotionAgentService>.Instance);

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private static StartPromotionAgentRequest StartRequest(int maxDiscount = 30) =>
        new() { Objective = Objective, MaxDiscountPercent = maxDiscount };

    private static string ProposalJson(
        Guid productId,
        string productName,
        decimal discount,
        int sold,
        int previous,
        int available,
        string type = "PercentageDiscount") =>
        JsonSerializer.Serialize(new PromotionProposalDocument
        {
            SchemaVersion = "1.0",
            Summary = "Test proposal.",
            Proposals = new List<PromotionProposalItem>
            {
                new()
                {
                    ProductId = productId,
                    ProductName = productName,
                    PromotionType = type,
                    DiscountValue = discount,
                    StartDate = Tomorrow,
                    EndDate = Tomorrow.AddDays(14),
                    Rationale = "Sales declined against the previous period.",
                    Evidence = new ProposalEvidence { UnitsSold = sold, PreviousUnitsSold = previous, AvailableQuantity = available }
                }
            }
        }, AgentJson.Options);

    private static string CarbonaraProposal(decimal discount = 15m, int available = 35) =>
        ProposalJson(SeedData.ProductCarbonara, "Spaghetti Carbonara", discount, 3, 4, available);

    // ---- Happy path --------------------------------------------------------------

    [Fact]
    public async Task StartAsync_ShouldProposeValidatedPromotion_ForDecliningProduct()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(carbonaraPrevious: 4, carbonaraCurrent: 3);

        var result = await h.Agent().StartAsync(StartRequest());

        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, result.Status);
        Assert.Equal(Objective, result.Objective);
        Assert.NotEmpty(result.Plan);
        Assert.Empty(result.Errors);

        // Margherita also declined but already has a live promotion, so only Carbonara is proposed.
        var proposal = Assert.Single(result.Proposal!.Proposals);
        Assert.Equal(SeedData.ProductCarbonara, proposal.ProductId);
        Assert.Equal("PercentageDiscount", proposal.PromotionType);
        Assert.Equal(15m, proposal.DiscountValue);                 // 25% decline -> 15%
        Assert.Equal(Tomorrow, proposal.StartDate);
        Assert.Equal(Tomorrow.AddDays(14), proposal.EndDate);
        Assert.Equal((3, 4, 35), (proposal.Evidence.UnitsSold, proposal.Evidence.PreviousUnitsSold, proposal.Evidence.AvailableQuantity));
        Assert.Equal(PromotionImpactLevel.Low, result.ImpactLevel);

        var price = Assert.Single(Assert.Single(result.Pricing).Variants);
        Assert.Equal(1800m, price.OriginalPrice);
        Assert.Equal(1530m, price.FinalPrice);

        Assert.All(result.ValidationResults, v => Assert.True(v.IsValid, v.Message));
        Assert.Contains(result.ValidationResults, v => v.ValidatorName == "NoConflict");
        Assert.Contains(result.ValidationResults, v => v.ValidatorName == "SufficientInventory");

        var tools = result.ToolExecutions.Select(t => t.ToolName).ToList();
        Assert.Equal(
            new[]
            {
                "GetSalesVelocity", "GetActivePromotions", "GetInventory", "GetProductDetails",
                "GetProductPricing", "SubmitPromotionProposal", "CalculatePromotion"
            },
            tools);
        Assert.All(result.ToolExecutions, t => Assert.Equal(AgentToolStatus.Success, t.Status));

        var approval = Assert.Single(result.Approvals);
        Assert.Equal(ApprovalStatus.Pending, approval.Status);

        // Nothing is written before approval.
        Assert.Equal(4, await h.Context.Promotions.CountAsync());
    }

    [Fact]
    public async Task StartAsync_ShouldStoreOnlyStructuredProposal_NotReasoning()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);

        var result = await h.Agent().StartAsync(StartRequest());

        var submission = result.ToolExecutions.Single(t => t.ToolName == "SubmitPromotionProposal");
        var keys = submission.Arguments!.AsObject().Select(p => p.Key).ToList();
        Assert.Equal(new[] { "schemaVersion", "summary", "proposals" }, keys);

        var stored = await h.Context.AgentWorkflows.SingleAsync(w => w.Id == result.WorkflowId);
        Assert.Contains("GetSalesVelocity", stored.PlanSummary);
    }

    [Fact]
    public async Task StartAsync_ShouldCompleteWithoutApproval_WhenNoProductQualifies()
    {
        await using var h = await Harness.CreateAsync();

        var result = await h.Agent().StartAsync(StartRequest());

        Assert.Equal(AgentWorkflowStatus.Completed, result.Status);
        Assert.Empty(result.Proposal!.Proposals);
        Assert.Empty(result.Approvals);
        Assert.Contains("nothing was changed", result.FinalOutcome);
    }

    // ---- Deterministic validation failures ------------------------------------------

    public static IEnumerable<object[]> InvalidProposals() => new[]
    {
        new object[] { "invalid product", "ProductExists" },
        new object[] { "insufficient inventory", "SufficientInventory" },
        new object[] { "invalid discount", "DiscountLimits" },
        new object[] { "fixed discount above limit", "DiscountLimits" },
        new object[] { "conflicting promotion", "NoConflict" },
        new object[] { "fabricated evidence", "EvidenceMatchesData" },
        new object[] { "free item", "ServerPricing" },
    };

    [Theory]
    [MemberData(nameof(InvalidProposals))]
    public async Task StartAsync_ShouldFailClosed_WhenProposalBreaksABusinessRule(string scenario, string failedRule)
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);

        string output;
        switch (scenario)
        {
            case "insufficient inventory":
                var stock = await h.Context.Inventory.SingleAsync(i => i.ProductVariantId == SeedData.VariantCarbonaraRegular);
                stock.ReservedQuantity = 30; // 35 on hand -> 5 available, reorder level 10
                await h.Context.SaveChangesAsync();
                output = CarbonaraProposal(available: 5);
                break;
            case "invalid product":
                output = ProposalJson(Guid.NewGuid(), "Imaginary Pizza", 10m, 0, 0, 0);
                break;
            case "invalid discount":
                output = CarbonaraProposal(discount: 80m);
                break;
            case "fixed discount above limit":
                output = ProposalJson(SeedData.ProductCarbonara, "Spaghetti Carbonara", 900m, 3, 4, 35, "FixedAmountDiscount");
                break;
            case "conflicting promotion":
                output = ProposalJson(SeedData.ProductMargherita, "Margherita Pizza", 10m, 2, 10, 80);
                break;
            case "fabricated evidence":
                output = ProposalJson(SeedData.ProductCarbonara, "Spaghetti Carbonara", 15m, 1, 99, 35);
                break;
            default: // free item: 100% passes the % limit only if the limit allows it
                h.AgentOptions.HighImpactDiscountPercent = 20;
                output = ProposalJson(SeedData.ProductCarbonara, "Spaghetti Carbonara", 1800m, 3, 4, 35, "FixedAmountDiscount");
                break;
        }

        var request = StartRequest(maxDiscount: scenario == "free item" ? 50 : 30);
        var result = await h.Agent(FakeModel.Returning(output)).StartAsync(request);

        Assert.Equal(AgentWorkflowStatus.Failed, result.Status);
        Assert.Contains(result.ValidationResults, v => v.ValidatorName == failedRule && !v.IsValid);
        Assert.Equal("ValidationFailed", Assert.Single(result.Errors).ErrorType);
        Assert.Empty(result.Approvals);
        Assert.Equal(4, await h.Context.Promotions.CountAsync());
    }

    // ---- Malformed model output -------------------------------------------------

    [Theory]
    [InlineData("Sure! I recommend 20% off Carbonara because sales dropped.")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"summary\":\"x\",\"proposals\":[],\"reasoning\":\"step by step...\"}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"summary\":\"x\",\"proposals\":[{\"productId\":\"00000000-0000-0000-0000-000000000023\",\"productName\":\"Carbonara\",\"promotionType\":\"FreeMoney\",\"discountValue\":10,\"startDate\":\"2026-10-16T00:00:00Z\",\"endDate\":\"2026-10-30T00:00:00Z\",\"rationale\":\"Sales declined recently.\",\"evidence\":{\"unitsSold\":3,\"previousUnitsSold\":4,\"availableQuantity\":35}}]}")]
    public async Task StartAsync_ShouldRejectMalformedModelOutput_WithoutStoringIt(string output)
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);

        var result = await h.Agent(FakeModel.Returning(output)).StartAsync(StartRequest());

        Assert.Equal(AgentWorkflowStatus.Failed, result.Status);
        Assert.Equal("MalformedOutput", Assert.Single(result.Errors).ErrorType);

        var submission = result.ToolExecutions.Single(t => t.ToolName == "SubmitPromotionProposal");
        Assert.Equal(AgentToolStatus.Failed, submission.Status);
        Assert.Null(submission.Arguments);
        Assert.Contains("not stored", submission.ErrorMessage);
        Assert.Empty(result.Approvals);
    }

    // ---- Tool failures, timeouts and safe failure -----------------------------------

    [Fact]
    public async Task StartAsync_ShouldRetryThenFailSafely_WhenToolKeepsFailing()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);
        var broken = new FakeTool("GetInventory", _ => throw new InvalidOperationException("connection string leaked"));

        var result = await h.Agent(null, broken).StartAsync(StartRequest());

        Assert.Equal(AgentWorkflowStatus.Failed, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal("ToolFailure", error.ErrorType);
        Assert.DoesNotContain("leaked", error.Message);

        Assert.Equal(3, broken.Calls); // first attempt + 2 bounded retries
        var attempts = result.ToolExecutions.Where(t => t.ToolName == "GetInventory").ToList();
        Assert.Equal(3, attempts.Count);
        Assert.All(attempts, a => Assert.Equal(AgentToolStatus.Failed, a.Status));
        Assert.Contains("no promotions were created", result.FinalOutcome);
    }

    [Fact]
    public async Task StartAsync_ShouldFailSafely_WhenToolTimesOut()
    {
        await using var h = await Harness.CreateAsync();
        h.AgentOptions.ToolTimeout = TimeSpan.FromMilliseconds(50);
        h.AgentOptions.MaxToolRetries = 1;
        var slow = new FakeTool("GetSalesVelocity", async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new { items = Array.Empty<object>() };
        });

        var result = await h.Agent(null, slow).StartAsync(StartRequest());

        Assert.Equal(AgentWorkflowStatus.Failed, result.Status);
        Assert.Equal("ToolTimeout", Assert.Single(result.Errors).ErrorType);
        Assert.Equal(2, slow.Calls);
        Assert.All(result.ToolExecutions, t => Assert.Contains("Timed out", t.ErrorMessage));
    }

    [Fact]
    public async Task StartAsync_ShouldFailSafely_WhenModelTimesOut()
    {
        await using var h = await Harness.CreateAsync();
        h.AgentOptions.ModelTimeout = TimeSpan.FromMilliseconds(50);
        var slowModel = new FakeModel(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return string.Empty;
        });

        var result = await h.Agent(slowModel).StartAsync(StartRequest());

        Assert.Equal(AgentWorkflowStatus.Failed, result.Status);
        Assert.Equal("ModelTimeout", Assert.Single(result.Errors).ErrorType);
    }

    [Fact]
    public async Task StartAsync_ShouldHideInternalDetails_WhenModelThrows()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);
        var crashing = new FakeModel((_, _) => throw new InvalidOperationException("internal secret detail"));

        var result = await h.Agent(crashing).StartAsync(StartRequest());

        Assert.Equal(AgentWorkflowStatus.Failed, result.Status);
        var error = Assert.Single(result.Errors);
        Assert.Equal("UnexpectedError", error.ErrorType);
        Assert.DoesNotContain("secret", error.Message);
        Assert.Equal(4, await h.Context.Promotions.CountAsync());

        // The failure itself is persisted.
        var stored = await h.Context.AgentWorkflows.Include(w => w.Errors).SingleAsync(w => w.Id == result.WorkflowId);
        Assert.Equal(AgentWorkflowStatus.Failed, stored.Status);
        Assert.Single(stored.Errors);
    }

    // ---- Tool permissions and input validation -----------------------------------

    private static async Task<AgentWorkflowStep> NewStepAsync(Harness h)
    {
        var workflow = new AgentWorkflow { Objective = "Registry test", Status = AgentWorkflowStatus.InProgress };
        var step = new AgentWorkflowStep
        {
            StepOrder = 1,
            AgentName = PromotionAgentConstants.AgentName,
            Title = "Registry test",
            Status = AgentStepStatus.Running
        };
        workflow.Steps.Add(step);
        h.Context.AgentWorkflows.Add(workflow);
        await h.Context.SaveChangesAsync();
        return step;
    }

    [Theory]
    [InlineData("UpdatePromotion")]
    [InlineData("ExecuteSql")]
    [InlineData("CallExternalApi")]
    public async Task Registry_ShouldBlockToolsOutsideTheAllowList(string toolName)
    {
        await using var h = await Harness.CreateAsync();
        var step = await NewStepAsync(h);
        // Even a registered implementation is refused if it is not allow-listed.
        var rogue = new FakeTool(toolName, _ => Task.FromResult<object>(new { items = Array.Empty<object>() }));

        await Assert.ThrowsAsync<ToolPermissionException>(() =>
            h.Registry(rogue).ExecuteAsync(step, toolName, new JsonObject(), CancellationToken.None));

        Assert.Equal(0, rogue.Calls);
        var record = Assert.Single(step.ToolExecutions);
        Assert.Equal(AgentToolStatus.Failed, record.Status);
        Assert.DoesNotContain(toolName, PromotionAgentConstants.AllowedTools);
    }

    [Theory]
    [InlineData("{\"analysisDays\":500}")]
    [InlineData("{\"analysisDays\":\"thirty\"}")]
    [InlineData("{\"analysisDays\":30,\"sql\":\"DROP TABLE Promotions\"}")]
    [InlineData("{}")]
    public async Task Registry_ShouldRejectInvalidToolInput_WithoutRetrying(string arguments)
    {
        await using var h = await Harness.CreateAsync();
        var step = await NewStepAsync(h);

        var ex = await Assert.ThrowsAsync<PromotionAgentToolException>(() =>
            h.Registry().ExecuteAsync(step, "GetSalesVelocity", JsonNode.Parse(arguments)!.AsObject(), CancellationToken.None));

        Assert.True(ex.IsRejected);
        Assert.Single(step.ToolExecutions); // not retried
    }

    [Fact]
    public async Task Registry_ShouldRejectToolOutputMissingRequiredData()
    {
        await using var h = await Harness.CreateAsync();
        var step = await NewStepAsync(h);
        var wrongShape = new FakeTool("GetInventory", _ => Task.FromResult<object>(new { rows = 1 }));

        var ex = await Assert.ThrowsAsync<PromotionAgentToolException>(() =>
            h.Registry(wrongShape).ExecuteAsync(step, "GetInventory",
                new JsonObject { ["productIds"] = new JsonArray(SeedData.ProductCarbonara.ToString()) },
                CancellationToken.None));

        Assert.True(ex.IsRejected);
    }

    [Fact]
    public void JsonRedactor_ShouldRemoveSecretsAtAnyDepth()
    {
        var node = JsonNode.Parse("""
            {"apiKey":"k","nested":{"password":"p","ok":1},"list":[{"accessToken":"t","sku":"PIZ"}]}
            """);

        var redacted = JsonRedactor.Redact(node)!.ToJsonString();

        Assert.DoesNotContain("\"k\"", redacted);
        Assert.DoesNotContain("\"p\"", redacted);
        Assert.DoesNotContain("\"t\"", redacted);
        Assert.Contains("PIZ", redacted);
        Assert.Contains(JsonRedactor.Placeholder, redacted);
    }

    // ---- Approval, rejection and revision --------------------------------------------

    [Fact]
    public async Task ApproveAsync_ShouldCreatePromotionThroughPromotionService()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());

        var result = await agent.ApproveAsync(started.WorkflowId, h.StaffUserId, reviewerIsAdministrator: false, "Looks good.");

        Assert.Equal(AgentWorkflowStatus.Completed, result!.Status);
        var approval = Assert.Single(result.Approvals);
        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Equal(h.StaffUserId, approval.ReviewedByUserId);
        Assert.Equal("Looks good.", approval.Comment);

        var promotionId = Assert.Single(result.CreatedPromotionIds);
        var promotion = await h.Context.Promotions.Include(p => p.PromotionProducts).SingleAsync(p => p.Id == promotionId);
        Assert.Equal("Spaghetti Carbonara 15% off", promotion.Name);
        Assert.Equal(PromotionType.PercentageDiscount, promotion.Type);
        Assert.Equal(15m, promotion.DiscountValue);
        Assert.Equal(Tomorrow, promotion.StartDate);
        Assert.True(promotion.IsActive);
        Assert.Equal(SeedData.ProductCarbonara, Assert.Single(promotion.PromotionProducts).ProductId);

        Assert.Contains(result.Steps, s => s.Title.StartsWith("Re-validate") && s.Status == AgentStepStatus.Completed);
        Assert.Contains(result.Steps, s => s.AgentName == PromotionAgentConstants.ExecutorName);
        Assert.Contains("Created 1 promotion", result.FinalOutcome);
    }

    [Fact]
    public async Task ApproveAsync_ShouldRequireAdministrator_ForHighImpactProposal()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(carbonaraPrevious: 10, carbonaraCurrent: 2); // 80% decline -> 20%
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());

        Assert.Equal(PromotionImpactLevel.High, started.ImpactLevel);
        Assert.Equal(20m, started.Proposal!.Proposals.Single().DiscountValue);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.ApproveAsync(started.WorkflowId, h.StaffUserId, reviewerIsAdministrator: false, null));

        var stillWaiting = await agent.GetAsync(started.WorkflowId);
        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, stillWaiting!.Status);

        var approved = await agent.ApproveAsync(started.WorkflowId, h.AdminUserId, reviewerIsAdministrator: true, null);
        Assert.Equal(AgentWorkflowStatus.Completed, approved!.Status);
        Assert.Single(approved.CreatedPromotionIds);
    }

    [Fact]
    public async Task RejectAsync_ShouldCancelWorkflow_WithoutChanges()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());

        var result = await agent.RejectAsync(started.WorkflowId, h.StaffUserId, "Not this month.");

        Assert.Equal(AgentWorkflowStatus.Cancelled, result!.Status);
        var approval = Assert.Single(result.Approvals);
        Assert.Equal(ApprovalStatus.Rejected, approval.Status);
        Assert.Equal("Not this month.", approval.Comment);
        Assert.Empty(result.CreatedPromotionIds);
        Assert.Equal(4, await h.Context.Promotions.CountAsync());
    }

    [Fact]
    public async Task ReviseAsync_ShouldProduceNewProposalUnderReviewerConstraints()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(10, 2);
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());

        var result = await agent.ReviseAsync(started.WorkflowId, h.AdminUserId, new RevisePromotionAgentRequest
        {
            Comment = "Keep discounts at 10% or less.",
            MaxDiscountPercent = 10
        });

        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, result!.Status);
        Assert.Equal(10m, result.Proposal!.Proposals.Single().DiscountValue);
        Assert.Equal(PromotionImpactLevel.Low, result.ImpactLevel);
        Assert.Equal(1530m + 90m, result.Pricing.Single().Variants.Single().FinalPrice); // 1800 - 10%

        Assert.Equal(
            new[] { ApprovalStatus.RevisionRequested, ApprovalStatus.Pending },
            result.Approvals.Select(a => a.Status));
        Assert.Equal("Keep discounts at 10% or less.", result.Approvals[0].Comment);
        Assert.Equal(2, result.ToolExecutions.Count(t => t.ToolName == "SubmitPromotionProposal"));
    }

    [Fact]
    public async Task ReviseAsync_ShouldCompleteWithoutProposal_WhenReviewerExcludesAllCandidates()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());

        var result = await agent.ReviseAsync(started.WorkflowId, h.StaffUserId, new RevisePromotionAgentRequest
        {
            Comment = "Do not discount Carbonara.",
            ExcludeProductIds = new List<Guid> { SeedData.ProductCarbonara }
        });

        Assert.Equal(AgentWorkflowStatus.Completed, result!.Status);
        Assert.Empty(result.Proposal!.Proposals);
    }

    [Fact]
    public async Task ReviseAsync_ShouldEnforceRevisionLimit()
    {
        await using var h = await Harness.CreateAsync();
        h.AgentOptions.MaxRevisions = 1;
        await h.SeedDecliningSalesAsync(4, 3);
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());
        var revise = new RevisePromotionAgentRequest { Comment = "Try again." };

        await agent.ReviseAsync(started.WorkflowId, h.StaffUserId, revise);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.ReviseAsync(started.WorkflowId, h.StaffUserId, revise));
    }

    [Fact]
    public async Task Decisions_ShouldOnlyBeAllowed_WhileAwaitingApproval()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());
        await agent.ApproveAsync(started.WorkflowId, h.StaffUserId, false, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.ApproveAsync(started.WorkflowId, h.StaffUserId, false, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.RejectAsync(started.WorkflowId, h.StaffUserId, null));

        Assert.Equal(5, await h.Context.Promotions.CountAsync()); // created exactly once
    }

    [Fact]
    public async Task ApproveAsync_ShouldFailSafely_WhenProposalBecameStale()
    {
        await using var h = await Harness.CreateAsync();
        await h.SeedDecliningSalesAsync(4, 3);
        var agent = h.Agent();
        var started = await agent.StartAsync(StartRequest());

        // Someone launches a Carbonara promotion before the proposal is approved.
        await h.PromotionService().CreatePromotionAsync(new PromotionRequest
        {
            Name = "Manual pasta deal",
            Type = PromotionType.PercentageDiscount,
            DiscountValue = 5m,
            StartDate = Now.AddDays(-1),
            EndDate = Now.AddDays(10),
            IsActive = true,
            ProductIds = new List<Guid> { SeedData.ProductCarbonara }
        });

        var result = await agent.ApproveAsync(started.WorkflowId, h.StaffUserId, false, null);

        Assert.Equal(AgentWorkflowStatus.Failed, result!.Status);
        Assert.Equal("StaleProposal", Assert.Single(result.Errors).ErrorType);
        Assert.Contains(result.ValidationResults, v => v.ValidatorName == "NoConflict" && !v.IsValid);
        Assert.Empty(result.CreatedPromotionIds);
        Assert.Equal(5, await h.Context.Promotions.CountAsync()); // only the manual one was added
    }

    [Fact]
    public async Task UnknownWorkflow_ShouldReturnNull()
    {
        await using var h = await Harness.CreateAsync();
        var agent = h.Agent();

        Assert.Null(await agent.GetAsync(Guid.NewGuid()));
        Assert.Null(await agent.ApproveAsync(Guid.NewGuid(), h.StaffUserId, true, null));
        Assert.Null(await agent.RejectAsync(Guid.NewGuid(), h.StaffUserId, null));
    }
}
