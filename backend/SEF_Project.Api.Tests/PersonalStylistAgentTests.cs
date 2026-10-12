using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Recommendations;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Tests;

public class PersonalStylistAgentTests
{
    [Fact]
    public async Task Agent_UsesOnlyAllowListedToolsAndReturnsGroundedRecommendations()
    {
        var fixture = AgentFixture.Create();
        fixture.Wishlist.Output = new WishlistToolOutput(new[]
        {
            new WishlistToolItem(
                fixture.Product.ProductId,
                fixture.Product.Name,
                IsAvailable: true)
        });
        var agent = fixture.CreateAgent();

        var result = await agent.RunAsync(7, fixture.Context);

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal(fixture.Product.ProductId, recommendation.ProductId);
        Assert.Equal(fixture.Variant.Id, recommendation.VariantId);
        Assert.Equal(fixture.Availability.Price, recommendation.Price);
        Assert.Contains("wishlist", recommendation.Reason);
        Assert.Equal("completed", result.Execution.Status);
        Assert.True(result.Execution.OutputValidated);
        Assert.Equal(4, result.Execution.ToolAttempts);
        Assert.Equal(4, result.Execution.SuccessfulToolExecutions);
        Assert.Equal(
            PersonalStylistToolNames.Allowed.OrderBy(item => item),
            fixture.Recorder.ToolNames.OrderBy(item => item));
        Assert.True(fixture.Recorder.Validation?.IsValid);
        Assert.True(fixture.Recorder.Completed);
        Assert.False(fixture.Recorder.Failed);
    }

    [Fact]
    public async Task GroundedModel_SelectsVariantMatchingRequestedSizeAndColour()
    {
        var productId = Guid.NewGuid();
        var cheapWrongVariant = new RecommendationCatalogVariant(
            Guid.NewGuid(), "SHIRT-L", "Large", 1000m, 5, Size: "L", Colour: "Black");
        var matchingVariant = new RecommendationCatalogVariant(
            Guid.NewGuid(), "SHIRT-M", "Medium", 2000m, 5, Size: "M", Colour: "Navy");
        var product = new RecommendationCatalogProduct(
            productId,
            "Shirt",
            "A shirt",
            1000m,
            Array.Empty<RecommendationCatalogCategory>(),
            new[] { cheapWrongVariant, matchingVariant });

        var available = new[]
        {
            new VerifiedProductAvailability(
                productId,
                cheapWrongVariant.Id,
                product.Name,
                cheapWrongVariant.Name,
                cheapWrongVariant.Sku,
                cheapWrongVariant.Price,
                cheapWrongVariant.AvailableQuantity,
                cheapWrongVariant.Size,
                cheapWrongVariant.Colour),
            new VerifiedProductAvailability(
                productId,
                matchingVariant.Id,
                product.Name,
                matchingVariant.Name,
                matchingVariant.Sku,
                matchingVariant.Price,
                matchingVariant.AvailableQuantity,
                matchingVariant.Size,
                matchingVariant.Colour)
        };

        var input = new PersonalStylistModelInput(
            new RecommendationCustomerContext(7, "Demo", "Customer"),
            new RecommendationContext("Casual", null, new[] { "Navy" }, "M", null),
            Array.Empty<WishlistToolItem>(),
            new[] { product },
            available,
            Array.Empty<string>());

        var output = await new GroundedPersonalStylistModel().GenerateAsync(input);

        var recommendation = Assert.Single(output.Recommendations);
        Assert.Equal(matchingVariant.Id, recommendation.VariantId);
        Assert.Equal("M", recommendation.Size);
        Assert.Equal("Navy", recommendation.Colour);
    }

    [Fact]
    public async Task Agent_SuppliesOnlyVerifiedMBlackVariantToModel()
    {
        var fixture = AgentFixture.Create();
        var productId = Guid.NewGuid();
        var matching = new RecommendationCatalogVariant(
            Guid.NewGuid(), "SHIRT-M-BLACK", "Medium / Black", 1800m, 9, "M", "Black");
        var wrongSize = new RecommendationCatalogVariant(
            Guid.NewGuid(), "SHIRT-L-BLACK", "Large / Black", 1600m, 5, "L", "Black");
        var wrongColour = new RecommendationCatalogVariant(
            Guid.NewGuid(), "SHIRT-M-RED", "Medium / Red", 1700m, 5, "M", "Red");
        var unverified = new RecommendationCatalogVariant(
            Guid.NewGuid(), "SHIRT-M-BLACK-2", "Medium / Black 2", 1500m, 5, "M", "Black");
        var product = new RecommendationCatalogProduct(
            productId,
            "Casual Shirt",
            "Relaxed cotton shirt",
            1500m,
            Array.Empty<RecommendationCatalogCategory>(),
            new[] { matching, wrongSize, wrongColour, unverified });
        var matchingAvailability = AvailabilityFor(
            product,
            matching,
            authoritativePrice: 2000m,
            authoritativeQuantity: 3);
        ConfigureCandidates(
            fixture,
            new RecommendationContext(
                "Casual Outing", 20000m, new[] { "Black" }, "M", null),
            product,
            matchingAvailability,
            AvailabilityFor(product, wrongSize),
            AvailabilityFor(product, wrongColour));
        PersonalStylistModelInput? capturedInput = null;
        var model = new DelegateModel((input, _) =>
        {
            capturedInput = input;
            var selected = Assert.Single(input.AvailableVariants);
            return Task.FromResult(new PersonalStylistModelOutput(new[]
            {
                DraftFor(selected)
            }));
        });

        var result = await fixture.CreateAgent(model: model).RunAsync(7, fixture.Context);

        var modelInput = Assert.IsType<PersonalStylistModelInput>(capturedInput);
        var available = Assert.Single(modelInput.AvailableVariants);
        var catalogueVariant = Assert.Single(
            Assert.Single(modelInput.CatalogueProducts).AvailableVariants);
        Assert.Equal(matching.Id, available.VariantId);
        Assert.Equal(matching.Id, catalogueVariant.Id);
        Assert.Equal(matchingAvailability.Price, catalogueVariant.Price);
        Assert.Equal(matchingAvailability.AvailableQuantity, catalogueVariant.AvailableQuantity);
        Assert.DoesNotContain(modelInput.AvailableVariants, item =>
            item.VariantId == wrongSize.Id ||
            item.VariantId == wrongColour.Id ||
            item.VariantId == unverified.Id);
        Assert.Equal(matching.Id, Assert.Single(result.Recommendations).VariantId);
        Assert.True(result.Execution.OutputValidated);
    }

    [Fact]
    public async Task Agent_ReturnsValidatedEmptyResultWithoutCallingModel_WhenNoVariantMatches()
    {
        var fixture = AgentFixture.Create();
        var productId = Guid.NewGuid();
        var wrongVariant = new RecommendationCatalogVariant(
            Guid.NewGuid(), "SHIRT-L-RED", "Large / Red", 2500m, 4, "L", "Red");
        var product = new RecommendationCatalogProduct(
            productId,
            "Casual Shirt",
            "Relaxed cotton shirt",
            wrongVariant.Price,
            Array.Empty<RecommendationCatalogCategory>(),
            new[] { wrongVariant });
        ConfigureCandidates(
            fixture,
            new RecommendationContext(
                "Casual Outing", 20000m, new[] { "Black" }, "M", null),
            product,
            AvailabilityFor(product, wrongVariant));
        var modelCalled = false;
        var model = new DelegateModel((_, _) =>
        {
            modelCalled = true;
            throw new InvalidOperationException("The model must not be called.");
        });

        var result = await fixture.CreateAgent(model: model).RunAsync(7, fixture.Context);

        Assert.False(modelCalled);
        Assert.Empty(result.Recommendations);
        Assert.Equal("completed", result.Execution.Status);
        Assert.True(result.Execution.OutputValidated);
        Assert.True(fixture.Recorder.Completed);
        Assert.False(fixture.Recorder.Failed);
    }

    [Fact]
    public async Task Agent_RemovesUnavailableInvalidPriceAndOverBudgetVariants()
    {
        var fixture = AgentFixture.Create();
        var productId = Guid.NewGuid();
        var noStock = new RecommendationCatalogVariant(
            Guid.NewGuid(), "NO-STOCK", "No stock", 1000m, 1, "M", "Black");
        var invalidPrice = new RecommendationCatalogVariant(
            Guid.NewGuid(), "INVALID-PRICE", "Invalid price", 1000m, 1, "M", "Black");
        var overBudget = new RecommendationCatalogVariant(
            Guid.NewGuid(), "OVER-BUDGET", "Over budget", 1000m, 1, "M", "Black");
        var product = new RecommendationCatalogProduct(
            productId,
            "Filtered Shirt",
            null,
            1000m,
            Array.Empty<RecommendationCatalogCategory>(),
            new[] { noStock, invalidPrice, overBudget });
        ConfigureCandidates(
            fixture,
            new RecommendationContext(
                "Casual Outing", 2000m, new[] { "Black" }, "M", null),
            product,
            AvailabilityFor(product, noStock, authoritativeQuantity: 0),
            AvailabilityFor(product, invalidPrice, authoritativePrice: 0m),
            AvailabilityFor(product, overBudget, authoritativePrice: 2001m));
        var modelCalled = false;
        var model = new DelegateModel((_, _) =>
        {
            modelCalled = true;
            return Task.FromResult(new PersonalStylistModelOutput(
                Array.Empty<PersonalStylistDraftRecommendation>()));
        });

        var result = await fixture.CreateAgent(model: model).RunAsync(7, fixture.Context);

        Assert.False(modelCalled);
        Assert.Empty(result.Recommendations);
        Assert.True(result.Execution.OutputValidated);
    }

    [Fact]
    public async Task Agent_DoesNotExposeVariantBeyondFirstFiftyAvailabilityCandidates()
    {
        var fixture = AgentFixture.Create();
        var productId = Guid.NewGuid();
        var variants = Enumerable.Range(1, 51)
            .Select(index => new RecommendationCatalogVariant(
                Guid.NewGuid(),
                $"SHIRT-{index}",
                $"Variant {index}",
                1000m + index,
                2,
                index == 51 ? "M" : "L",
                index == 51 ? "Black" : "Red"))
            .ToList();
        var product = new RecommendationCatalogProduct(
            productId,
            "Many Variant Shirt",
            null,
            variants.Min(item => item.Price),
            Array.Empty<RecommendationCatalogCategory>(),
            variants);
        var verifiedFirstFifty = variants
            .Take(50)
            .Select(variant => AvailabilityFor(product, variant))
            .ToArray();
        ConfigureCandidates(
            fixture,
            new RecommendationContext(
                "Casual Outing", 20000m, new[] { "Black" }, "M", null),
            product,
            verifiedFirstFifty);
        var modelCalled = false;
        var model = new DelegateModel((_, _) =>
        {
            modelCalled = true;
            return Task.FromResult(new PersonalStylistModelOutput(
                Array.Empty<PersonalStylistDraftRecommendation>()));
        });

        var result = await fixture.CreateAgent(model: model).RunAsync(7, fixture.Context);

        Assert.False(modelCalled);
        Assert.Equal(
            PersonalStylistAgentContract.MaximumCandidateVariants,
            fixture.AvailabilityTool.LastInput?.Items.Count);
        Assert.DoesNotContain(
            fixture.AvailabilityTool.LastInput!.Items,
            item => item.VariantId == variants[50].Id);
        Assert.Empty(result.Recommendations);
        Assert.True(result.Execution.OutputValidated);
    }

    [Fact]
    [Trait("TestSuite", "PersonalStylistGolden")]
    public async Task Agent_RejectsMalformedModelOutputAndFailsSafely()
    {
        var fixture = AgentFixture.Create();
        var model = new DelegateModel((_, _) => Task.FromResult(
            new PersonalStylistModelOutput(new[]
            {
                new PersonalStylistDraftRecommendation(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    fixture.Availability.Price,
                    1,
                    fixture.Availability.Size,
                    fixture.Availability.Colour,
                    "Invented recommendation")
            })));
        var agent = fixture.CreateAgent(model: model);

        var result = await agent.RunAsync(7, fixture.Context);

        Assert.Empty(result.Recommendations);
        Assert.Equal("failed", result.Execution.Status);
        Assert.False(result.Execution.OutputValidated);
        Assert.Contains("validation", result.Execution.ErrorSummary!);
        Assert.False(fixture.Recorder.Validation?.IsValid);
        Assert.True(fixture.Recorder.Failed);
        Assert.Equal("MalformedOutput", fixture.Recorder.ErrorType);
    }

    [Fact]
    public async Task Agent_RetriesTransientToolFailureWithinConfiguredLimit()
    {
        var fixture = AgentFixture.Create();
        fixture.Customer.TransientFailuresRemaining = 1;
        var agent = fixture.CreateAgent(maximumToolAttempts: 2);

        var result = await agent.RunAsync(7, fixture.Context);

        Assert.Equal("completed", result.Execution.Status);
        Assert.Equal(2, fixture.Customer.Calls);
        Assert.Equal(5, result.Execution.ToolAttempts);
        Assert.Equal(4, result.Execution.SuccessfulToolExecutions);
        Assert.Equal(
            2,
            fixture.Recorder.ToolNames.Count(name =>
                name == PersonalStylistToolNames.CustomerPreference));
    }

    [Fact]
    [Trait("TestSuite", "PersonalStylistGolden")]
    public async Task Agent_DoesNotRetryPermanentToolFailureAndReturnsNoProducts()
    {
        var fixture = AgentFixture.Create();
        fixture.Customer.PermanentFailure = true;
        var agent = fixture.CreateAgent(maximumToolAttempts: 3);

        var result = await agent.RunAsync(7, fixture.Context);

        Assert.Empty(result.Recommendations);
        Assert.Equal("failed", result.Execution.Status);
        Assert.Equal(1, fixture.Customer.Calls);
        Assert.Equal(1, result.Execution.ToolAttempts);
        Assert.Equal("ToolFailure", fixture.Recorder.ErrorType);
    }

    [Fact]
    [Trait("TestSuite", "PersonalStylistGolden")]
    public async Task Agent_ToolTimeoutReturnsStructuredSafeFailure()
    {
        var fixture = AgentFixture.Create();
        fixture.Customer.Delay = TimeSpan.FromSeconds(5);
        var agent = fixture.CreateAgent(
            maximumToolAttempts: 1,
            toolTimeoutSeconds: 1);

        var result = await agent.RunAsync(7, fixture.Context);

        Assert.Empty(result.Recommendations);
        Assert.Equal("failed", result.Execution.Status);
        Assert.Contains("failed", result.Execution.ErrorSummary!);
        Assert.Equal(1, result.Execution.ToolAttempts);
        Assert.Equal("ToolFailure", fixture.Recorder.ErrorType);
    }

    [Fact]
    public async Task CompleteRecommendationWorkflow_PersistsPlanDelegationAndAuditableResult()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var fixture = AgentFixture.Create();
        var recorder = new AgentWorkflowRecorder(context);
        var agent = fixture.CreateAgent(recorder: recorder);

        var response = await new RecommendationService(agent).StartAsync(
            7,
            new RecommendationRequest
            {
                Occasion = fixture.Context.Occasion,
                Budget = fixture.Context.Budget,
                PreferredColours = fixture.Context.PreferredColours,
                PreferredSize = fixture.Context.PreferredSize,
                StylePreferences = fixture.Context.StylePreferences
            });

        context.ChangeTracker.Clear();
        var workflow = await context.AgentWorkflows
            .AsNoTracking()
            .Include(item => item.Steps)
                .ThenInclude(step => step.ToolExecutions)
            .Include(item => item.Steps)
                .ThenInclude(step => step.ValidationResults)
            .Include(item => item.Errors)
            .SingleAsync(item => item.Id == response.WorkflowId);
        var coordinatorStep = workflow.Steps.Single(step =>
            step.AgentName == PersonalStylistAgentContract.CoordinatorAgentName);
        var step = workflow.Steps.Single(step =>
            step.AgentName == PersonalStylistAgentContract.AgentName);

        Assert.Equal(AgentWorkflowStatus.Completed, workflow.Status);
        Assert.Equal("completed", response.Status);
        Assert.Single(response.Recommendations);
        Assert.Contains("Wedding", workflow.Objective);
        Assert.Contains("steps", workflow.PlanSummary);
        Assert.Equal(AgentStepStatus.Completed, coordinatorStep.Status);
        Assert.Equal(1, coordinatorStep.StepOrder);
        Assert.Equal(AgentStepStatus.Completed, step.Status);
        Assert.Equal(2, step.StepOrder);
        Assert.Equal(PersonalStylistAgentContract.AgentName, step.AgentName);
        Assert.Equal(4, step.ToolExecutions.Count);
        Assert.All(step.ToolExecutions, execution =>
            Assert.Equal(AgentToolStatus.Success, execution.Status));
        Assert.Equal(11, step.ValidationResults.Count);
        Assert.All(step.ValidationResults, validation =>
            Assert.True(validation.IsValid));
        Assert.Empty(workflow.Errors);
        Assert.Contains(fixture.Product.ProductId.ToString(), workflow.FinalOutcome);
        Assert.DoesNotContain("chain-of-thought", workflow.FinalOutcome);
    }

    [Fact]
    public async Task Agent_PersistsMalformedOutputAsFailedWorkflowWithoutRecommendations()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var fixture = AgentFixture.Create();
        var recorder = new AgentWorkflowRecorder(context);
        var logger = new CapturingLogger<PersonalStylistAgent>();
        var malformedModel = new DelegateModel((_, _) => Task.FromResult(
            new PersonalStylistModelOutput(new[]
            {
                new PersonalStylistDraftRecommendation(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    fixture.Availability.Price,
                    1,
                    fixture.Availability.Size,
                    fixture.Availability.Colour,
                    "Unknown product")
            })));
        var agent = fixture.CreateAgent(
            model: malformedModel,
            recorder: recorder,
            logger: logger);

        var result = await agent.RunAsync(7, fixture.Context);

        context.ChangeTracker.Clear();
        var workflow = await context.AgentWorkflows
            .AsNoTracking()
            .Include(item => item.Steps)
                .ThenInclude(step => step.ValidationResults)
            .Include(item => item.Errors)
            .SingleAsync(item => item.Id == result.WorkflowId);
        var step = workflow.Steps.Single(item =>
            item.AgentName == PersonalStylistAgentContract.AgentName);

        Assert.Null(logger.LastError);
        Assert.Empty(result.Recommendations);
        Assert.Equal(2, workflow.Steps.Count);
        Assert.Equal(AgentWorkflowStatus.Failed, workflow.Status);
        Assert.Equal(AgentStepStatus.Failed, step.Status);
        Assert.Equal(11, step.ValidationResults.Count);
        Assert.Contains(step.ValidationResults, validation =>
            !validation.IsValid);
        Assert.Equal("MalformedOutput", Assert.Single(workflow.Errors).ErrorType);
        Assert.Equal("No recommendations returned.", workflow.FinalOutcome);
    }

    [Fact]
    public async Task Agent_AppendsVisibleStepToExistingWorkflowWithoutFinalizingIt()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var workflowId = Guid.NewGuid();
        var existingPlan = "{\"steps\":[\"coordinate\",\"personal-stylist\",\"inventory-promotion\",\"validation\"]}";
        context.AgentWorkflows.Add(new SEF_Project.Api.Models.AgenticAI.AgentWorkflow
        {
            Id = workflowId,
            Objective = "Prepare an outfit for a wedding.",
            PlanSummary = existingPlan,
            Status = AgentWorkflowStatus.Planning,
            Steps =
            {
                new SEF_Project.Api.Models.AgenticAI.AgentWorkflowStep
                {
                    Id = Guid.NewGuid(),
                    StepOrder = 1,
                    AgentName = "Planning / Coordinator Agent",
                    Title = "Plan customer objective",
                    Status = AgentStepStatus.Completed,
                    Summary = "Delegated fashion discovery.",
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                }
            }
        });
        await context.SaveChangesAsync();
        var fixture = AgentFixture.Create();
        var agent = fixture.CreateAgent(
            recorder: new AgentWorkflowRecorder(context));

        var result = await agent.RunWithinWorkflowAsync(
            workflowId,
            7,
            fixture.Context);

        context.ChangeTracker.Clear();
        var workflow = await context.AgentWorkflows
            .AsNoTracking()
            .Include(item => item.Steps)
                .ThenInclude(step => step.ToolExecutions)
            .Include(item => item.Steps)
                .ThenInclude(step => step.ValidationResults)
            .SingleAsync(item => item.Id == workflowId);
        var stylistStep = workflow.Steps.Single(step =>
            step.AgentName == PersonalStylistAgentContract.AgentName);

        Assert.Equal(workflowId, result.WorkflowId);
        Assert.Equal(AgentWorkflowStatus.InProgress, workflow.Status);
        Assert.Equal("Prepare an outfit for a wedding.", workflow.Objective);
        Assert.Equal(existingPlan, workflow.PlanSummary);
        Assert.Null(workflow.FinalOutcome);
        Assert.Equal(2, stylistStep.StepOrder);
        Assert.Equal(AgentStepStatus.Completed, stylistStep.Status);
        Assert.Equal(4, stylistStep.ToolExecutions.Count);
        Assert.Equal(11, stylistStep.ValidationResults.Count);
        Assert.All(stylistStep.ValidationResults, validation =>
            Assert.True(validation.IsValid));
    }

    [Fact]
    [Trait("TestSuite", "PersonalStylistGolden")]
    public async Task WorkflowRecorder_RejectsNonAllowListedTool()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var recorder = new AgentWorkflowRecorder(context);
        var workflow = await recorder.StartAsync("Test controlled tool allow-list.");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            recorder.StartToolAsync(
                workflow,
                "modify_inventory",
                "{}"));

        Assert.Empty(await context.AgentToolExecutions.ToListAsync());
    }

    private static async Task<AppDbContext> CreateContextAsync(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static void ConfigureCandidates(
        AgentFixture fixture,
        RecommendationContext context,
        RecommendationCatalogProduct product,
        params VerifiedProductAvailability[] availability)
    {
        fixture.Context = context;
        fixture.Search = new FakeProductSearchTool(new ProductSearchToolOutput(
            new[] { product },
            new RecommendationCatalogCapabilities(true, true)));
        fixture.AvailabilityTool = new FakeAvailabilityTool(
            new ProductAvailabilityToolOutput(availability));
    }

    private static VerifiedProductAvailability AvailabilityFor(
        RecommendationCatalogProduct product,
        RecommendationCatalogVariant variant,
        decimal? authoritativePrice = null,
        int? authoritativeQuantity = null) =>
        new(
            product.ProductId,
            variant.Id,
            product.Name,
            variant.Name,
            variant.Sku,
            authoritativePrice ?? variant.Price,
            authoritativeQuantity ?? variant.AvailableQuantity,
            variant.Size,
            variant.Colour);

    private static PersonalStylistDraftRecommendation DraftFor(
        VerifiedProductAvailability availability) =>
        new(
            availability.ProductId,
            availability.VariantId,
            availability.Price,
            1,
            availability.Size,
            availability.Colour,
            "Matches the requested preferences.");

    private sealed class AgentFixture
    {
        private AgentFixture()
        {
        }

        public RecommendationContext Context { get; set; } = new(
            "Wedding",
            20000m,
            new[] { "Navy" },
            "M",
            "Classic");

        public RecommendationCatalogVariant Variant { get; private init; } = null!;

        public RecommendationCatalogProduct Product { get; private init; } = null!;

        public VerifiedProductAvailability Availability { get; private init; } = null!;

        public FakeCustomerTool Customer { get; private init; } = null!;

        public FakeWishlistTool Wishlist { get; private init; } = null!;

        public FakeProductSearchTool Search { get; set; } = null!;

        public FakeAvailabilityTool AvailabilityTool { get; set; } = null!;

        public RecordingWorkflowRecorder Recorder { get; } = new();

        public static AgentFixture Create()
        {
            var productId = Guid.NewGuid();
            var variant = new RecommendationCatalogVariant(
                Guid.NewGuid(),
                "FORMAL-M",
                "Medium",
                15000m,
                3,
                Size: "M",
                Colour: "Navy");
            var product = new RecommendationCatalogProduct(
                productId,
                "Formal Jacket",
                "Tailored jacket",
                15000m,
                Array.Empty<RecommendationCatalogCategory>(),
                new[] { variant });
            var availability = new VerifiedProductAvailability(
                productId,
                variant.Id,
                product.Name,
                variant.Name,
                variant.Sku,
                variant.Price,
                variant.AvailableQuantity,
                variant.Size,
                variant.Colour);

            return new AgentFixture
            {
                Variant = variant,
                Product = product,
                Availability = availability,
                Customer = new FakeCustomerTool(),
                Wishlist = new FakeWishlistTool(),
                Search = new FakeProductSearchTool(new ProductSearchToolOutput(
                    new[] { product },
                    new RecommendationCatalogCapabilities(true, true))),
                AvailabilityTool = new FakeAvailabilityTool(
                    new ProductAvailabilityToolOutput(new[] { availability }))
            };
        }

        public PersonalStylistAgent CreateAgent(
            IPersonalStylistRecommendationModel? model = null,
            IAgentWorkflowRecorder? recorder = null,
            int maximumToolAttempts = 2,
            int toolTimeoutSeconds = 5,
            ILogger<PersonalStylistAgent>? logger = null) =>
            new(
                Customer,
                Search,
                Wishlist,
                AvailabilityTool,
                model ?? new GroundedPersonalStylistModel(),
                new PersonalStylistOutputValidator(),
                recorder ?? Recorder,
                Options.Create(new PersonalStylistAgentOptions
                {
                    OverallTimeoutSeconds = 20,
                    ToolTimeoutSeconds = toolTimeoutSeconds,
                    MaximumToolAttempts = maximumToolAttempts
                }),
                logger ?? NullLogger<PersonalStylistAgent>.Instance);
    }

    private sealed class FakeCustomerTool : ICustomerPreferenceTool
    {
        public int Calls { get; private set; }

        public int TransientFailuresRemaining { get; set; }

        public bool PermanentFailure { get; set; }

        public TimeSpan Delay { get; set; }

        public async Task<CustomerPreferenceToolOutput> ExecuteAsync(
            CustomerPreferenceToolInput input,
            CancellationToken cancellationToken = default)
        {
            Calls++;

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (TransientFailuresRemaining > 0)
            {
                TransientFailuresRemaining--;
                throw new PersonalStylistToolException(
                    PersonalStylistToolNames.CustomerPreference,
                    "Temporary failure.",
                    isTransient: true);
            }

            if (PermanentFailure)
            {
                throw new InvalidOperationException("Permanent failure.");
            }

            return new CustomerPreferenceToolOutput(
                new RecommendationCustomerContext(3, "Test", "Customer"),
                input.RequestedPreferences);
        }
    }

    private sealed class FakeWishlistTool : IWishlistTool
    {
        public WishlistToolOutput Output { get; set; } =
            new(Array.Empty<WishlistToolItem>());

        public Task<WishlistToolOutput> ExecuteAsync(
            WishlistToolInput input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Output);
    }

    private sealed class FakeProductSearchTool : IProductSearchTool
    {
        private readonly Queue<ProductSearchToolOutput> _outputs;

        public FakeProductSearchTool(params ProductSearchToolOutput[] outputs)
        {
            _outputs = new Queue<ProductSearchToolOutput>(outputs);
        }

        public Task<ProductSearchToolOutput> ExecuteAsync(
            ProductSearchToolInput input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_outputs.Dequeue());
    }

    private sealed class FakeAvailabilityTool : IProductAvailabilityTool
    {
        private readonly ProductAvailabilityToolOutput _output;

        public FakeAvailabilityTool(ProductAvailabilityToolOutput output)
        {
            _output = output;
        }

        public ProductAvailabilityToolInput? LastInput { get; private set; }

        public Task<ProductAvailabilityToolOutput> ExecuteAsync(
            ProductAvailabilityToolInput input,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(_output);
        }
    }

    private sealed class DelegateModel : IPersonalStylistRecommendationModel
    {
        private readonly Func<PersonalStylistModelInput, CancellationToken,
            Task<PersonalStylistModelOutput>> _generate;

        public DelegateModel(
            Func<PersonalStylistModelInput, CancellationToken,
                Task<PersonalStylistModelOutput>> generate)
        {
            _generate = generate;
        }

        public Task<PersonalStylistModelOutput> GenerateAsync(
            PersonalStylistModelInput input,
            CancellationToken cancellationToken = default) =>
            _generate(input, cancellationToken);
    }

    private sealed class RecordingWorkflowRecorder : IAgentWorkflowRecorder
    {
        private readonly Dictionary<Guid, string> _tools = new();

        public List<string> ToolNames { get; } = new();

        public RecommendationValidationCheck? Validation { get; private set; }

        public bool Completed { get; private set; }

        public bool Failed { get; private set; }

        public string? ErrorType { get; private set; }

        public Task<AgentWorkflowHandle> StartAsync(
            string objective,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentWorkflowHandle(
                Guid.NewGuid(),
                Guid.NewGuid()));

        public Task<AgentWorkflowHandle> AttachAsync(
            Guid workflowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentWorkflowHandle(
                workflowId,
                Guid.NewGuid(),
                CompletesWorkflow: false));

        public Task<AgentToolExecutionHandle> StartToolAsync(
            AgentWorkflowHandle workflow,
            string toolName,
            string argumentsJson,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            _tools[id] = toolName;
            ToolNames.Add(toolName);
            return Task.FromResult(new AgentToolExecutionHandle(id));
        }

        public Task CompleteToolAsync(
            AgentToolExecutionHandle execution,
            bool succeeded,
            string? resultJson,
            string? errorSummary,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RecordValidationAsync(
            AgentWorkflowHandle workflow,
            RecommendationValidationCheck check,
            CancellationToken cancellationToken = default)
        {
            Validation = check;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            AgentWorkflowHandle workflow,
            string finalResultJson,
            CancellationToken cancellationToken = default)
        {
            Completed = true;
            return Task.CompletedTask;
        }

        public Task FailAsync(
            AgentWorkflowHandle workflow,
            string errorType,
            string errorSummary,
            CancellationToken cancellationToken = default)
        {
            Failed = true;
            ErrorType = errorType;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? LastError { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                LastError = exception;
            }
        }
    }
}
