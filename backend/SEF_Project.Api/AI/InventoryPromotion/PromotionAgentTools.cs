using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using SEF_Project.Api.DTOs.Analytics;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;
using SEF_Project.Api.Services.Analytics;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.AI.InventoryPromotion;

/// <summary>
/// A controlled, read-only capability of the agent. Tools call existing
/// ASP.NET Core business services; none of them receives the DbContext or can
/// write data.
/// </summary>
public interface IPromotionAgentTool
{
    string Name { get; }

    /// <summary>Property every successful result must contain (output validation).</summary>
    string RequiredOutputProperty { get; }

    Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken);
}

/// <summary>Strict argument parsing for tools (input validation).</summary>
internal static class ToolArgs
{
    public static void AllowOnly(JsonObject args, params string[] names)
    {
        foreach (var (key, _) in args)
        {
            if (!names.Contains(key, StringComparer.Ordinal))
            {
                throw new ToolInputException($"Unknown argument '{key}'.");
            }
        }
    }

    public static int Int(JsonObject args, string name, int min, int max, int? fallback = null)
    {
        var node = args[name];

        if (node is null)
        {
            return fallback ?? throw new ToolInputException($"'{name}' is required.");
        }

        var value = Read<int>(node, name, "an integer");

        return value < min || value > max
            ? throw new ToolInputException($"'{name}' must be between {min} and {max}.")
            : value;
    }

    public static decimal Decimal(JsonObject args, string name, decimal min, decimal max)
    {
        var value = Read<decimal>(Required(args, name), name, "a number");

        return value < min || value > max
            ? throw new ToolInputException($"'{name}' must be between {min} and {max}.")
            : value;
    }

    public static Guid Guid(JsonObject args, string name)
    {
        var value = Read<Guid>(Required(args, name), name, "a GUID");

        return value == System.Guid.Empty
            ? throw new ToolInputException($"'{name}' must not be empty.")
            : value;
    }

    public static List<Guid> GuidList(JsonObject args, string name, int minCount, int maxCount)
    {
        var values = Read<List<Guid>>(Required(args, name), name, "a list of GUIDs");

        if (values.Count < minCount || values.Count > maxCount)
        {
            throw new ToolInputException($"'{name}' must contain {minCount} to {maxCount} items.");
        }

        if (values.Any(id => id == System.Guid.Empty))
        {
            throw new ToolInputException($"'{name}' must not contain empty GUIDs.");
        }

        return values.Distinct().ToList();
    }

    public static DateTime Date(JsonObject args, string name)
    {
        var value = Read<DateTime>(Required(args, name), name, "an ISO-8601 date");

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public static string OneOf(JsonObject args, string name, IReadOnlySet<string> allowed)
    {
        var value = Read<string>(Required(args, name), name, "a string");

        return allowed.Contains(value)
            ? value
            : throw new ToolInputException($"'{name}' must be one of: {string.Join(", ", allowed)}.");
    }

    private static JsonNode Required(JsonObject args, string name) =>
        args[name] ?? throw new ToolInputException($"'{name}' is required.");

    private static T Read<T>(JsonNode node, string name, string expected)
    {
        try
        {
            return node.Deserialize<T>(AgentJson.Options)
                ?? throw new ToolInputException($"'{name}' must be {expected}.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NotSupportedException)
        {
            throw new ToolInputException($"'{name}' must be {expected}.");
        }
    }
}

public sealed class GetSalesVelocityTool : IPromotionAgentTool
{
    private readonly IAnalyticsService _analytics;
    private readonly TimeProvider _timeProvider;

    public GetSalesVelocityTool(IAnalyticsService analytics, TimeProvider timeProvider)
    {
        _analytics = analytics;
        _timeProvider = timeProvider;
    }

    public string Name => PromotionAgentConstants.GetSalesVelocity;

    public string RequiredOutputProperty => "items";

    public async Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "analysisDays", "limit");
        var days = ToolArgs.Int(arguments, "analysisDays", 7, 90);
        var limit = ToolArgs.Int(arguments, "limit", 1, 100, 100);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var demand = await _analytics.GetDemandInsightsAsync(new DemandQuery
        {
            From = now.AddDays(-days),
            To = now,
            SortBy = "trend",
            SortDirection = "asc",
            PageSize = limit
        }, cancellationToken);

        return new
        {
            demand.From,
            demand.To,
            AnalysisDays = days,
            Items = demand.Items.Select(i => new
            {
                i.ProductId,
                i.ProductVariantId,
                i.ProductName,
                i.Sku,
                i.UnitsSold,
                i.PreviousUnitsSold,
                i.UnitsPerDay,
                i.TrendPercent,
                Trend = i.Trend.ToString(),
                i.AvailableQuantity,
                i.DaysOfCover
            }).ToList()
        };
    }
}

public sealed class GetInventoryTool : IPromotionAgentTool
{
    private const int PageSize = 100;
    private const int MaxPages = 10;

    private readonly IAnalyticsService _analytics;

    public GetInventoryTool(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    public string Name => PromotionAgentConstants.GetInventory;

    public string RequiredOutputProperty => "items";

    public async Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "productIds");
        var productIds = ToolArgs.GuidList(arguments, "productIds", 1, 50).ToHashSet();
        var items = new List<InventoryStockItem>();

        for (var page = 1; page <= MaxPages; page++)
        {
            var stock = await _analytics.GetInventoryStockAsync(new InventoryStockQuery
            {
                Page = page,
                PageSize = PageSize,
                IncludeInactive = true,
                SortBy = "sku",
                SortDirection = "asc"
            }, cancellationToken);

            items.AddRange(stock.Items.Where(item => productIds.Contains(item.ProductId)));

            if (page * PageSize >= stock.TotalItems)
            {
                break;
            }
        }

        return new
        {
            Items = items.Select(i => new
            {
                i.ProductId,
                i.ProductVariantId,
                i.Sku,
                i.IsActive,
                i.QuantityOnHand,
                i.ReservedQuantity,
                i.AvailableQuantity,
                i.ReorderLevel,
                StockStatus = i.StockStatus.ToString()
            }).ToList()
        };
    }
}

public sealed class GetActivePromotionsTool : IPromotionAgentTool
{
    private readonly IPromotionService _promotions;

    public GetActivePromotionsTool(IPromotionService promotions)
    {
        _promotions = promotions;
    }

    public string Name => PromotionAgentConstants.GetActivePromotions;

    public string RequiredOutputProperty => "items";

    public async Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments);

        // Customer visibility = live promotions only (active, in dates, campaign active).
        var live = await _promotions.GetPromotionsAsync(
            canManagePromotions: false,
            new PromotionQuery { PageSize = 100, SortBy = "endDate", SortDirection = "asc" },
            cancellationToken);

        return new
        {
            Items = live.Items.Select(p => new
            {
                p.Id,
                p.Name,
                Type = p.Type.ToString(),
                p.DiscountValue,
                p.StartDate,
                p.EndDate,
                p.ProductIds,
                p.CategoryIds
            }).ToList()
        };
    }
}

public sealed class GetProductDetailsTool : IPromotionAgentTool
{
    private readonly IPromotionOfferService _offers;

    public GetProductDetailsTool(IPromotionOfferService offers)
    {
        _offers = offers;
    }

    public string Name => PromotionAgentConstants.GetProductDetails;

    public string RequiredOutputProperty => "products";

    public async Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "productIds");
        var productIds = ToolArgs.GuidList(arguments, "productIds", 1, 50);
        var products = new List<object>();
        var missing = new List<Guid>();

        foreach (var productId in productIds)
        {
            var product = await _offers.GetProductPromotionsAsync(productId, cancellationToken);

            if (product is null)
            {
                missing.Add(productId);
                continue;
            }

            products.Add(new
            {
                product.ProductId,
                product.ProductName,
                product.Description,
                product.HasActivePromotion,
                ActivePromotionIds = product.Promotions.Select(p => p.Id).ToList()
            });
        }

        return new { Products = products, MissingProductIds = missing };
    }
}

public sealed class GetProductPricingTool : IPromotionAgentTool
{
    private readonly IPromotionOfferService _offers;

    public GetProductPricingTool(IPromotionOfferService offers)
    {
        _offers = offers;
    }

    public string Name => PromotionAgentConstants.GetProductPricing;

    public string RequiredOutputProperty => "products";

    public async Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "productIds");
        var productIds = ToolArgs.GuidList(arguments, "productIds", 1, 50);
        var products = new List<object>();
        var missing = new List<Guid>();

        foreach (var productId in productIds)
        {
            var product = await _offers.GetProductPromotionsAsync(productId, cancellationToken);

            if (product is null)
            {
                missing.Add(productId);
                continue;
            }

            products.Add(new
            {
                product.ProductId,
                Variants = product.Variants.Select(v => new
                {
                    v.ProductVariantId,
                    v.Sku,
                    v.Name,
                    Price = v.OriginalPrice,
                    v.Currency
                }).ToList()
            });
        }

        return new { Products = products, MissingProductIds = missing };
    }
}

/// <summary>
/// Prices a hypothetical promotion with the same PromotionDiscountCalculator
/// the API uses for real promotions. Nothing is saved.
/// </summary>
public sealed class CalculatePromotionTool : IPromotionAgentTool
{
    private readonly IPromotionOfferService _offers;

    public CalculatePromotionTool(IPromotionOfferService offers)
    {
        _offers = offers;
    }

    public string Name => PromotionAgentConstants.CalculatePromotion;

    public string RequiredOutputProperty => "variants";

    public async Task<object> ExecuteAsync(JsonObject arguments, CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "productId", "promotionType", "discountValue", "startDate", "endDate");
        var productId = ToolArgs.Guid(arguments, "productId");
        var type = ToolArgs.OneOf(arguments, "promotionType", PromotionAgentConstants.AllowedPromotionTypes);
        var discountValue = ToolArgs.Decimal(arguments, "discountValue", 0.01m, 1_000_000m);
        var startDate = ToolArgs.Date(arguments, "startDate");
        var endDate = ToolArgs.Date(arguments, "endDate");

        var product = await _offers.GetProductPromotionsAsync(productId, cancellationToken)
            ?? throw new ToolRejectedException("Product not found or inactive.");

        var promotion = new Promotion
        {
            Name = "Proposed promotion",
            Type = Enum.Parse<PromotionType>(type),
            DiscountValue = discountValue,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true
        };

        try
        {
            return new
            {
                ProductId = productId,
                Variants = product.Variants.Select(variant =>
                {
                    var price = PromotionDiscountCalculator.Calculate(promotion, variant.OriginalPrice, startDate);

                    return new
                    {
                        variant.ProductVariantId,
                        variant.Sku,
                        price.OriginalPrice,
                        price.DiscountAmount,
                        price.FinalPrice
                    };
                }).ToList()
            };
        }
        catch (InvalidOperationException ex)
        {
            throw new ToolRejectedException(ex.Message);
        }
    }
}

/// <summary>
/// Executes agent tools with permission checks, input/output validation,
/// per-call timeouts, bounded retries for transient failures, secret
/// redaction and an audit record (AgentToolExecution) for every attempt.
/// </summary>
public sealed class PromotionAgentToolRegistry
{
    private readonly Dictionary<string, IPromotionAgentTool> _tools;
    private readonly InventoryPromotionAgentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PromotionAgentToolRegistry> _logger;

    public PromotionAgentToolRegistry(
        IEnumerable<IPromotionAgentTool> tools,
        IOptions<InventoryPromotionAgentOptions> options,
        TimeProvider timeProvider,
        ILogger<PromotionAgentToolRegistry> logger)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<JsonNode> ExecuteAsync(
        AgentWorkflowStep step,
        string toolName,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        var argumentsJson = AgentJson.Serialize(JsonRedactor.Redact(arguments));

        if (!PromotionAgentConstants.AllowedTools.Contains(toolName)
            || !_tools.TryGetValue(toolName, out var tool))
        {
            var now = Now();
            Record(step, toolName, argumentsJson, null, AgentToolStatus.Failed,
                "Tool is not permitted for this agent.", now, now);
            _logger.LogWarning("Blocked non-permitted tool {ToolName} in workflow {WorkflowId}.", toolName, step.WorkflowId);
            throw new ToolPermissionException(toolName);
        }

        var maxAttempts = 1 + Math.Max(0, _options.MaxToolRetries);

        for (var attempt = 1; ; attempt++)
        {
            var startedAt = Now();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ToolTimeout);

            try
            {
                var raw = await tool
                    .ExecuteAsync((JsonObject)arguments.DeepClone(), timeout.Token)
                    .WaitAsync(timeout.Token);

                var result = ValidateOutput(tool, raw);

                Record(step, toolName, argumentsJson, AgentJson.Serialize(JsonRedactor.Redact(result)),
                    AgentToolStatus.Success, null, startedAt, Now());

                _logger.LogInformation(
                    "Tool {ToolName} succeeded on attempt {Attempt} for workflow {WorkflowId}.",
                    toolName, attempt, step.WorkflowId);

                return result;
            }
            catch (Exception ex) when (ex is ToolInputException or ToolRejectedException or ArgumentException)
            {
                // Deterministic failures: retrying cannot help.
                Record(step, toolName, argumentsJson, null, AgentToolStatus.Failed, ex.Message, startedAt, Now());
                throw new PromotionAgentToolException(toolName, isTimeout: false,
                    $"{toolName} rejected the request: {ex.Message}", isRejected: true);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Record(step, toolName, argumentsJson, null, AgentToolStatus.Failed,
                    $"Timed out after {_options.ToolTimeout.TotalMilliseconds:0} ms.", startedAt, Now());

                if (attempt >= maxAttempts)
                {
                    throw new PromotionAgentToolException(toolName, isTimeout: true,
                        $"{toolName} timed out after {attempt} attempt(s).");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Tool {ToolName} failed on attempt {Attempt}.", toolName, attempt);
                Record(step, toolName, argumentsJson, null, AgentToolStatus.Failed,
                    "The tool failed unexpectedly.", startedAt, Now());

                if (attempt >= maxAttempts)
                {
                    throw new PromotionAgentToolException(toolName, isTimeout: false,
                        $"{toolName} failed after {attempt} attempt(s).");
                }
            }
        }
    }

    private JsonNode ValidateOutput(IPromotionAgentTool tool, object raw)
    {
        var node = AgentJson.ToNode(raw);

        if (node is not JsonObject obj || !obj.ContainsKey(tool.RequiredOutputProperty))
        {
            throw new ToolRejectedException($"{tool.Name} returned output without '{tool.RequiredOutputProperty}'.");
        }

        if (AgentJson.Serialize(node).Length > _options.MaxToolOutputCharacters)
        {
            throw new ToolRejectedException($"{tool.Name} returned too much data.");
        }

        return node;
    }

    private static void Record(
        AgentWorkflowStep step,
        string toolName,
        string? argumentsJson,
        string? resultJson,
        AgentToolStatus status,
        string? error,
        DateTime startedAt,
        DateTime completedAt)
    {
        step.ToolExecutions.Add(new AgentToolExecution
        {
            ToolName = toolName.Length > 200 ? toolName[..200] : toolName,
            ToolArgumentsJson = argumentsJson,
            ToolResultJson = resultJson,
            Status = status,
            ErrorMessage = error is { Length: > 2000 } ? error[..2000] : error,
            StartedAt = startedAt,
            CompletedAt = completedAt
        });
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
