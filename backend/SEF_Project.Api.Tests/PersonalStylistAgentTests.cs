using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Data;
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
    public async Task Agent_RejectsMalformedModelOutputAndFailsSafely()
    {
        var fixture = AgentFixture.Create();
        var model = new DelegateModel((_, _) => Task.FromResult(
            new PersonalStylistModelOutput(new[]
            {
                new PersonalStylistDraftRecommendation(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
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
    public async Task Agent_PersistsWorkflowStepToolsValidationAndFinalResult()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var fixture = AgentFixture.Create();
        var recorder = new AgentWorkflowRecorder(context);
        var agent = fixture.CreateAgent(recorder: recorder);

        var result = await agent.RunAsync(7, fixture.Context);

        context.ChangeTracker.Clear();
        var workflow = await context.AgentWorkflows
            .AsNoTracking()
            .Include(item => item.Steps)
                .ThenInclude(step => step.ToolExecutions)
            .Include(item => item.Steps)
                .ThenInclude(step => step.ValidationResults)
            .Include(item => item.Errors)
            .SingleAsync(item => item.Id == result.WorkflowId);
        var step = Assert.Single(workflow.Steps);

        Assert.Equal(AgentWorkflowStatus.Completed, workflow.Status);
        Assert.Equal(AgentStepStatus.Completed, step.Status);
        Assert.Equal(PersonalStylistAgentContract.AgentName, step.AgentName);
        Assert.Equal(4, step.ToolExecutions.Count);
        Assert.All(step.ToolExecutions, execution =>
            Assert.Equal(AgentToolStatus.Success, execution.Status));
        Assert.True(Assert.Single(step.ValidationResults).IsValid);
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
        var step = Assert.Single(workflow.Steps);

        Assert.Null(logger.LastError);
        Assert.Empty(result.Recommendations);
        Assert.Equal(AgentWorkflowStatus.Failed, workflow.Status);
        Assert.Equal(AgentStepStatus.Failed, step.Status);
        Assert.False(Assert.Single(step.ValidationResults).IsValid);
        Assert.Equal("MalformedOutput", Assert.Single(workflow.Errors).ErrorType);
        Assert.Equal("No recommendations returned.", workflow.FinalOutcome);
    }

    [Fact]
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

    private sealed class AgentFixture
    {
        private AgentFixture()
        {
        }

        public RecommendationContext Context { get; } = new(
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

        public FakeProductSearchTool Search { get; private init; } = null!;

        public FakeAvailabilityTool AvailabilityTool { get; private init; } = null!;

        public RecordingWorkflowRecorder Recorder { get; } = new();

        public static AgentFixture Create()
        {
            var productId = Guid.NewGuid();
            var variant = new RecommendationCatalogVariant(
                Guid.NewGuid(),
                "FORMAL-M",
                "Medium",
                15000m,
                3);
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
                variant.AvailableQuantity);

            return new AgentFixture
            {
                Variant = variant,
                Product = product,
                Availability = availability,
                Customer = new FakeCustomerTool(),
                Wishlist = new FakeWishlistTool(),
                Search = new FakeProductSearchTool(new ProductSearchToolOutput(
                    new[] { product },
                    new RecommendationCatalogCapabilities(false, false))),
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

        public Task<ProductAvailabilityToolOutput> ExecuteAsync(
            ProductAvailabilityToolInput input,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_output);
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

        public AgentOutputValidation? Validation { get; private set; }

        public bool Completed { get; private set; }

        public bool Failed { get; private set; }

        public string? ErrorType { get; private set; }

        public Task<AgentWorkflowHandle> StartAsync(
            string objective,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentWorkflowHandle(
                Guid.NewGuid(),
                Guid.NewGuid()));

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
            bool isValid,
            string message,
            CancellationToken cancellationToken = default)
        {
            Validation = new AgentOutputValidation(
                isValid,
                message,
                Array.Empty<PersonalStylistRecommendation>());
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
