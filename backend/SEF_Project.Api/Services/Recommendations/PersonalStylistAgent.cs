using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SEF_Project.Api.Services.Recommendations;

public class PersonalStylistAgent : IPersonalStylistAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ICustomerPreferenceTool _customerPreferenceTool;
    private readonly IProductSearchTool _productSearchTool;
    private readonly IWishlistTool _wishlistTool;
    private readonly IProductAvailabilityTool _availabilityTool;
    private readonly IPersonalStylistRecommendationModel _model;
    private readonly IPersonalStylistOutputValidator _outputValidator;
    private readonly IAgentWorkflowRecorder _workflowRecorder;
    private readonly PersonalStylistAgentOptions _options;
    private readonly ILogger<PersonalStylistAgent> _logger;

    public PersonalStylistAgent(
        ICustomerPreferenceTool customerPreferenceTool,
        IProductSearchTool productSearchTool,
        IWishlistTool wishlistTool,
        IProductAvailabilityTool availabilityTool,
        IPersonalStylistRecommendationModel model,
        IPersonalStylistOutputValidator outputValidator,
        IAgentWorkflowRecorder workflowRecorder,
        IOptions<PersonalStylistAgentOptions> options,
        ILogger<PersonalStylistAgent> logger)
    {
        _customerPreferenceTool = customerPreferenceTool;
        _productSearchTool = productSearchTool;
        _wishlistTool = wishlistTool;
        _availabilityTool = availabilityTool;
        _model = model;
        _outputValidator = outputValidator;
        _workflowRecorder = workflowRecorder;
        _options = options.Value;
        _options.Validate();
        _logger = logger;
    }

    public async Task<PersonalStylistAgentResult> RunAsync(
        int userId,
        RecommendationContext context,
        CancellationToken cancellationToken = default)
    {
        PersonalStylistToolValidation.ValidateUserId(userId);
        PersonalStylistToolValidation.ValidatePreferences(context);

        var workflow = await _workflowRecorder.StartAsync(
            "Generate grounded fashion product recommendations for the authenticated customer.",
            cancellationToken);
        var counters = new ExecutionCounters();
        RecommendationCustomerContext? customer = null;
        var unappliedPreferences = new List<string>();
        var relaxedCriteria = new List<string>();
        IReadOnlyList<RecommendationValidationCheck> validationChecks =
            Array.Empty<RecommendationValidationCheck>();

        using var overallTimeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(_options.OverallTimeoutSeconds));
        using var executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                overallTimeout.Token);

        try
        {
            var preferenceOutput = await ExecuteToolAsync(
                workflow,
                PersonalStylistToolNames.CustomerPreference,
                Json(new
                {
                    source = "authenticated_customer",
                    hasBudget = context.Budget.HasValue,
                    preferredColourCount = context.PreferredColours.Count,
                    hasPreferredSize = context.PreferredSize != null,
                    hasStylePreferences = context.StylePreferences != null
                }),
                token => _customerPreferenceTool.ExecuteAsync(
                    new CustomerPreferenceToolInput(userId, context),
                    token),
                _ => Json(new { customerContextLoaded = true }),
                counters,
                executionCancellation.Token);
            customer = preferenceOutput.Customer;

            var wishlist = await ExecuteToolAsync(
                workflow,
                PersonalStylistToolNames.Wishlist,
                Json(new { source = "authenticated_customer" }),
                token => _wishlistTool.ExecuteAsync(
                    new WishlistToolInput(userId),
                    token),
                output => Json(new { itemCount = output.Items.Count }),
                counters,
                executionCancellation.Token);

            var productSearch = await SearchProductsAsync(
                workflow,
                context.Occasion,
                context.Budget,
                counters,
                executionCancellation.Token);

            if (productSearch.Products.Count == 0)
            {
                productSearch = await SearchProductsAsync(
                    workflow,
                    searchText: null,
                    context.Budget,
                    counters,
                    executionCancellation.Token);
                relaxedCriteria.Add("occasion");
            }

            if (context.PreferredColours.Count > 0 &&
                !productSearch.Capabilities.SupportsColour)
            {
                unappliedPreferences.Add("preferredColours");
            }

            if (context.PreferredSize != null &&
                !productSearch.Capabilities.SupportsSize)
            {
                unappliedPreferences.Add("preferredSize");
            }

            if (context.StylePreferences != null)
            {
                unappliedPreferences.Add("stylePreferences");
            }

            var availability = productSearch.Products.Count == 0
                ? new ProductAvailabilityToolOutput(
                    Array.Empty<VerifiedProductAvailability>())
                : await VerifyAvailabilityAsync(
                    workflow,
                    productSearch.Products,
                    counters,
                    executionCancellation.Token);
            var modelInput = new PersonalStylistModelInput(
                customer,
                preferenceOutput.Preferences,
                wishlist.Items,
                productSearch.Products,
                availability.AvailableVariants,
                relaxedCriteria);
            var modelOutput = await GenerateModelOutputAsync(
                modelInput,
                executionCancellation.Token);
            var validation = _outputValidator.Validate(modelOutput, modelInput);
            validationChecks = validation.Checks;

            foreach (var check in validation.Checks)
            {
                await _workflowRecorder.RecordValidationAsync(
                    workflow,
                    check,
                    CancellationToken.None);
            }

            if (!validation.IsValid)
            {
                throw new InvalidDataException(
                    "The Personal Stylist Agent returned malformed or ungrounded output.");
            }

            await _workflowRecorder.CompleteAsync(
                workflow,
                Json(new
                {
                    recommendations = validation.Recommendations.Select(item => new
                    {
                        item.ProductId,
                        item.VariantId,
                        item.Price,
                        item.Quantity,
                        item.Size,
                        item.Colour,
                        item.Reason
                    })
                }),
                CancellationToken.None);

            _logger.LogInformation(
                "{AgentName} workflow {WorkflowId} completed with {Count} recommendations.",
                PersonalStylistAgentContract.AgentName,
                workflow.WorkflowId,
                validation.Recommendations.Count);

            return new PersonalStylistAgentResult(
                workflow.WorkflowId,
                customer,
                context,
                validation.Recommendations,
                unappliedPreferences,
                relaxedCriteria,
                new PersonalStylistExecutionSummary(
                    PersonalStylistAgentContract.AgentName,
                    "completed",
                    counters.Attempts,
                    counters.Successes,
                    OutputValidated: true,
                    ErrorSummary: null,
                    validationChecks));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryFailWorkflowAsync(
                workflow,
                "Cancelled",
                "The request was cancelled before completion.");
            throw;
        }
        catch (Exception exception)
        {
            var failure = ClassifyFailure(exception, overallTimeout.IsCancellationRequested);
            await TryFailWorkflowAsync(
                workflow,
                failure.ErrorType,
                failure.ErrorSummary);

            _logger.LogWarning(
                exception,
                "{AgentName} workflow {WorkflowId} failed safely as {ErrorType}.",
                PersonalStylistAgentContract.AgentName,
                workflow.WorkflowId,
                failure.ErrorType);

            return new PersonalStylistAgentResult(
                workflow.WorkflowId,
                customer,
                context,
                Array.Empty<PersonalStylistRecommendation>(),
                unappliedPreferences,
                relaxedCriteria,
                new PersonalStylistExecutionSummary(
                    PersonalStylistAgentContract.AgentName,
                    "failed",
                    counters.Attempts,
                    counters.Successes,
                    OutputValidated: false,
                    failure.ErrorSummary,
                    validationChecks));
        }
    }

    private Task<ProductSearchToolOutput> SearchProductsAsync(
        AgentWorkflowHandle workflow,
        string? searchText,
        decimal? maximumPrice,
        ExecutionCounters counters,
        CancellationToken cancellationToken) =>
        ExecuteToolAsync(
            workflow,
            PersonalStylistToolNames.ProductSearch,
            Json(new { searchText, maximumPrice, limit = 12 }),
            token => _productSearchTool.ExecuteAsync(
                new ProductSearchToolInput(searchText, maximumPrice),
                token),
            output => Json(new
            {
                productCount = output.Products.Count,
                productIds = output.Products.Select(item => item.ProductId)
            }),
            counters,
            cancellationToken);

    private Task<ProductAvailabilityToolOutput> VerifyAvailabilityAsync(
        AgentWorkflowHandle workflow,
        IReadOnlyList<RecommendationCatalogProduct> products,
        ExecutionCounters counters,
        CancellationToken cancellationToken)
    {
        var candidates = products
            .SelectMany(product => product.AvailableVariants.Select(variant =>
                new ProductAvailabilityToolItem(product.ProductId, variant.Id)))
            .Take(PersonalStylistAgentContract.MaximumCandidateVariants)
            .ToList();

        return ExecuteToolAsync(
            workflow,
            PersonalStylistToolNames.ProductAvailability,
            Json(new { candidateCount = candidates.Count }),
            token => _availabilityTool.ExecuteAsync(
                new ProductAvailabilityToolInput(candidates),
                token),
            output => Json(new
            {
                availableCount = output.AvailableVariants.Count,
                variantIds = output.AvailableVariants.Select(item => item.VariantId)
            }),
            counters,
            cancellationToken);
    }

    private async Task<TOutput> ExecuteToolAsync<TOutput>(
        AgentWorkflowHandle workflow,
        string toolName,
        string argumentsJson,
        Func<CancellationToken, Task<TOutput>> execute,
        Func<TOutput, string> summarize,
        ExecutionCounters counters,
        CancellationToken cancellationToken)
    {
        if (!PersonalStylistToolNames.Allowed.Contains(toolName))
        {
            throw new InvalidOperationException(
                $"Tool '{toolName}' is not allow-listed.");
        }

        for (var attempt = 1; attempt <= _options.MaximumToolAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            counters.Attempts++;
            var execution = await _workflowRecorder.StartToolAsync(
                workflow,
                toolName,
                argumentsJson,
                CancellationToken.None);
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(_options.ToolTimeoutSeconds));
            using var attemptCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeout.Token);

            try
            {
                var output = await execute(attemptCancellation.Token);
                await _workflowRecorder.CompleteToolAsync(
                    execution,
                    succeeded: true,
                    summarize(output),
                    errorSummary: null,
                    CancellationToken.None);
                counters.Successes++;
                return output;
            }
            catch (OperationCanceledException) when (
                timeout.IsCancellationRequested &&
                !cancellationToken.IsCancellationRequested)
            {
                await _workflowRecorder.CompleteToolAsync(
                    execution,
                    succeeded: false,
                    resultJson: null,
                    errorSummary: "Tool execution timed out.",
                    CancellationToken.None);

                if (attempt == _options.MaximumToolAttempts)
                {
                    throw new PersonalStylistToolException(
                        toolName,
                        "Tool execution timed out.",
                        isTransient: true);
                }
            }
            catch (OperationCanceledException)
            {
                await _workflowRecorder.CompleteToolAsync(
                    execution,
                    succeeded: false,
                    resultJson: null,
                    errorSummary: "Tool execution was cancelled.",
                    CancellationToken.None);
                throw;
            }
            catch (Exception exception)
            {
                var retryable = IsTransient(exception);
                await _workflowRecorder.CompleteToolAsync(
                    execution,
                    succeeded: false,
                    resultJson: null,
                    errorSummary: "Tool execution failed.",
                    CancellationToken.None);

                if (!retryable || attempt == _options.MaximumToolAttempts)
                {
                    throw new PersonalStylistToolException(
                        toolName,
                        "Tool execution failed.",
                        retryable,
                        exception);
                }
            }
        }

        throw new PersonalStylistToolException(
            toolName,
            "Tool retry limit was reached.");
    }

    private async Task<PersonalStylistModelOutput> GenerateModelOutputAsync(
        PersonalStylistModelInput input,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(_options.ToolTimeoutSeconds));
        using var modelCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);

        try
        {
            return await _model.GenerateAsync(input, modelCancellation.Token);
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Personal Stylist recommendation generation timed out.");
        }
    }

    private async Task TryFailWorkflowAsync(
        AgentWorkflowHandle workflow,
        string errorType,
        string errorSummary)
    {
        try
        {
            await _workflowRecorder.FailAsync(
                workflow,
                errorType,
                errorSummary,
                CancellationToken.None);
        }
        catch (Exception recorderException)
        {
            _logger.LogError(
                recorderException,
                "Could not persist failure for Personal Stylist workflow {WorkflowId}.",
                workflow.WorkflowId);
        }
    }

    private static (string ErrorType, string ErrorSummary) ClassifyFailure(
        Exception exception,
        bool overallTimedOut) =>
        overallTimedOut || exception is TimeoutException
            ? (
                "AgentTimeout",
                "The Personal Stylist Agent timed out and returned no recommendations.")
            : exception switch
            {
                PersonalStylistToolException => (
                    "ToolFailure",
                    "A controlled recommendation tool failed; no recommendations were returned."),
                InvalidDataException => (
                    "MalformedOutput",
                    "Agent output failed catalogue validation; no recommendations were returned."),
                _ => (
                    "AgentFailure",
                    "The Personal Stylist Agent failed safely and returned no recommendations.")
            };

    private static bool IsTransient(Exception exception) =>
        exception is TimeoutException or HttpRequestException or
            System.Data.Common.DbException ||
        exception is PersonalStylistToolException { IsTransient: true };

    private static string Json<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private sealed class ExecutionCounters
    {
        public int Attempts { get; set; }

        public int Successes { get; set; }
    }
}
