using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Data;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Orders;

namespace SEF_Project.Api.AI.FulfilmentException;

/// <summary>
/// A controlled, read-only capability of the agent. Tools read order,
/// payment, shipment and status-history data through AppDbContext; none of
/// them can write data or change an order's state.
/// </summary>
public interface IFulfilmentExceptionTool
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

    public static Guid Guid(JsonObject args, string name)
    {
        if (args[name] is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || !System.Guid.TryParse(text, out var guid)
            || guid == System.Guid.Empty)
        {
            throw new ToolInputException($"'{name}' must be a non-empty GUID.");
        }

        return guid;
    }
}

public sealed class GetOrderDetailsTool : IFulfilmentExceptionTool
{
    private readonly AppDbContext _context;

    public GetOrderDetailsTool(AppDbContext context)
    {
        _context = context;
    }

    public string Name => FulfilmentExceptionAgentConstants.GetOrderDetails;

    public string RequiredOutputProperty => "order";

    public async Task<object> ExecuteAsync(
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "orderId");
        var orderId = ToolArgs.Guid(arguments, "orderId");

        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.DeliveryAddress)
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new ToolRejectedException($"Order '{orderId}' was not found.");

        return new
        {
            order = new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                status = order.Status.ToString(),
                subtotal = order.Subtotal,
                discountTotal = order.DiscountTotal,
                taxAmount = order.TaxAmount,
                shippingFee = order.ShippingFee,
                total = order.Total,
                currency = order.Currency,
                placedAt = order.PlacedAt,
                customerId = order.CustomerId,
                items = order.Items.Select(item => new
                {
                    itemId = item.Id,
                    productVariantId = item.ProductVariantId,
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice,
                    lineTotal = item.LineTotal
                }).ToList(),
                deliveryAddress = order.DeliveryAddress == null
                    ? null
                    : new
                    {
                        fullName = order.DeliveryAddress.FullName,
                        line1 = order.DeliveryAddress.Line1,
                        line2 = order.DeliveryAddress.Line2,
                        city = order.DeliveryAddress.City,
                        province = order.DeliveryAddress.Province,
                        postalCode = order.DeliveryAddress.PostalCode,
                        country = order.DeliveryAddress.Country,
                        phone = order.DeliveryAddress.Phone
                    }
            }
        };
    }
}

public sealed class GetPaymentsTool : IFulfilmentExceptionTool
{
    private readonly AppDbContext _context;

    public GetPaymentsTool(AppDbContext context)
    {
        _context = context;
    }

    public string Name => FulfilmentExceptionAgentConstants.GetPayments;

    public string RequiredOutputProperty => "payments";

    public async Task<object> ExecuteAsync(
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "orderId");
        var orderId = ToolArgs.Guid(arguments, "orderId");

        if (!await _context.Orders.AsNoTracking().AnyAsync(
                o => o.Id == orderId,
                cancellationToken))
        {
            throw new ToolRejectedException($"Order '{orderId}' was not found.");
        }

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new
            {
                paymentId = p.Id,
                amount = p.Amount,
                method = p.Method.ToString(),
                status = p.Status.ToString(),
                transactionReference = p.TransactionReference,
                paidAt = p.PaidAt
            })
            .ToListAsync(cancellationToken);

        return new { payments };
    }
}

public sealed class GetShipmentsTool : IFulfilmentExceptionTool
{
    private readonly AppDbContext _context;

    public GetShipmentsTool(AppDbContext context)
    {
        _context = context;
    }

    public string Name => FulfilmentExceptionAgentConstants.GetShipments;

    public string RequiredOutputProperty => "shipments";

    public async Task<object> ExecuteAsync(
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "orderId");
        var orderId = ToolArgs.Guid(arguments, "orderId");

        if (!await _context.Orders.AsNoTracking().AnyAsync(
                o => o.Id == orderId,
                cancellationToken))
        {
            throw new ToolRejectedException($"Order '{orderId}' was not found.");
        }

        var shipments = await _context.Shipments
            .AsNoTracking()
            .Where(s => s.OrderId == orderId)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new
            {
                shipmentId = s.Id,
                status = s.Status.ToString(),
                carrier = s.Carrier,
                trackingNumber = s.TrackingNumber,
                shippedAt = s.ShippedAt,
                deliveredAt = s.DeliveredAt
            })
            .ToListAsync(cancellationToken);

        return new { shipments };
    }
}

public sealed class GetStatusHistoryTool : IFulfilmentExceptionTool
{
    private readonly AppDbContext _context;

    public GetStatusHistoryTool(AppDbContext context)
    {
        _context = context;
    }

    public string Name => FulfilmentExceptionAgentConstants.GetStatusHistory;

    public string RequiredOutputProperty => "history";

    public async Task<object> ExecuteAsync(
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "orderId");
        var orderId = ToolArgs.Guid(arguments, "orderId");

        if (!await _context.Orders.AsNoTracking().AnyAsync(
                o => o.Id == orderId,
                cancellationToken))
        {
            throw new ToolRejectedException($"Order '{orderId}' was not found.");
        }

        var history = await _context.OrderStatusHistory
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new
            {
                status = h.Status.ToString(),
                changedAt = h.ChangedAt,
                note = h.Note
            })
            .ToListAsync(cancellationToken);

        return new { history };
    }
}

public sealed class GetPermittedTransitionsTool : IFulfilmentExceptionTool
{
    private readonly AppDbContext _context;

    public GetPermittedTransitionsTool(AppDbContext context)
    {
        _context = context;
    }

    public string Name => FulfilmentExceptionAgentConstants.GetPermittedTransitions;

    public string RequiredOutputProperty => "transitions";

    public async Task<object> ExecuteAsync(
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        ToolArgs.AllowOnly(arguments, "orderId");
        var orderId = ToolArgs.Guid(arguments, "orderId");

        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Payments)
            .Include(o => o.Shipments)
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new ToolRejectedException($"Order '{orderId}' was not found.");

        var allowedOrder = OrderService.AllowedTransitions.TryGetValue(
            order.Status,
            out var orderTargets)
            ? orderTargets
            : Array.Empty<OrderStatus>();

        return new
        {
            transitions = new
            {
                order = new
                {
                    orderId = order.Id,
                    currentStatus = order.Status.ToString(),
                    allowed = allowedOrder.Select(status => status.ToString()).ToList()
                },
                payments = order.Payments.Select(payment => new
                {
                    paymentId = payment.Id,
                    currentStatus = payment.Status.ToString(),
                    allowed = (OrderService.AllowedPaymentTransitions.TryGetValue(
                            payment.Status,
                            out var paymentTargets)
                        ? paymentTargets
                        : Array.Empty<PaymentStatus>())
                        .Select(status => status.ToString())
                        .ToList()
                }).ToList(),
                shipments = order.Shipments.Select(shipment => new
                {
                    shipmentId = shipment.Id,
                    currentStatus = shipment.Status.ToString(),
                    allowed = (OrderService.AllowedShipmentTransitions.TryGetValue(
                            shipment.Status,
                            out var shipmentTargets)
                        ? shipmentTargets
                        : Array.Empty<ShipmentStatus>())
                        .Select(status => status.ToString())
                        .ToList()
                }).ToList()
            }
        };
    }
}

/// <summary>
/// Executes agent tools with permission checks, input/output validation,
/// per-call timeouts, bounded retries for transient failures, and an audit
/// record (AgentToolExecution) for every attempt.
/// </summary>
public sealed class FulfilmentExceptionToolRegistry
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, IFulfilmentExceptionTool> _tools;
    private readonly FulfilmentExceptionAgentOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FulfilmentExceptionToolRegistry> _logger;

    public FulfilmentExceptionToolRegistry(
        IEnumerable<IFulfilmentExceptionTool> tools,
        IOptions<FulfilmentExceptionAgentOptions> options,
        TimeProvider timeProvider,
        ILogger<FulfilmentExceptionToolRegistry> logger)
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
        var argumentsJson = arguments.ToJsonString();

        if (!FulfilmentExceptionAgentConstants.AllowedTools.Contains(toolName)
            || !_tools.TryGetValue(toolName, out var tool))
        {
            var now = Now();
            Record(step, toolName, argumentsJson, null, AgentToolStatus.Failed,
                "Tool is not permitted for this agent.", now, now);
            _logger.LogWarning(
                "Blocked non-permitted tool {ToolName} in workflow {WorkflowId}.",
                toolName,
                step.WorkflowId);
            throw new ToolPermissionException(toolName);
        }

        var maxAttempts = 1 + Math.Max(0, _options.MaxToolRetries);

        for (var attempt = 1; ; attempt++)
        {
            var startedAt = Now();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeout.CancelAfter(_options.ToolTimeout);

            try
            {
                var raw = await tool
                    .ExecuteAsync((JsonObject)arguments.DeepClone(), timeout.Token)
                    .WaitAsync(timeout.Token);

                var result = ValidateOutput(tool, raw);

                Record(step, toolName, argumentsJson, result.ToJsonString(),
                    AgentToolStatus.Success, null, startedAt, Now());

                _logger.LogInformation(
                    "Tool {ToolName} succeeded on attempt {Attempt} for workflow {WorkflowId}.",
                    toolName,
                    attempt,
                    step.WorkflowId);

                return result;
            }
            catch (Exception ex) when (ex is ToolInputException or ToolRejectedException or ArgumentException)
            {
                // Deterministic failures: retrying cannot help.
                Record(step, toolName, argumentsJson, null, AgentToolStatus.Failed,
                    ex.Message, startedAt, Now());
                throw new FulfilmentExceptionToolException(
                    toolName,
                    isTimeout: false,
                    $"{toolName} rejected the request: {ex.Message}",
                    isRejected: true);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Record(step, toolName, argumentsJson, null, AgentToolStatus.Failed,
                    $"Timed out after {_options.ToolTimeout.TotalMilliseconds:0} ms.",
                    startedAt, Now());

                if (attempt >= maxAttempts)
                {
                    throw new FulfilmentExceptionToolException(
                        toolName,
                        isTimeout: true,
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
                    throw new FulfilmentExceptionToolException(
                        toolName,
                        isTimeout: false,
                        $"{toolName} failed after {attempt} attempt(s).");
                }
            }
        }
    }

    private JsonNode ValidateOutput(IFulfilmentExceptionTool tool, object raw)
    {
        var node = JsonSerializer.SerializeToNode(raw, JsonOptions)
            ?? throw new ToolRejectedException($"{tool.Name} returned no output.");

        if (node is not JsonObject obj || !obj.ContainsKey(tool.RequiredOutputProperty))
        {
            throw new ToolRejectedException(
                $"{tool.Name} returned output without '{tool.RequiredOutputProperty}'.");
        }

        if (node.ToJsonString().Length > _options.MaxToolOutputCharacters)
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
