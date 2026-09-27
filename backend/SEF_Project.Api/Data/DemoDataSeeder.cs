using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.AI.InventoryPromotion;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Agents;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.AgenticAI;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Models.Shopping;
using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Data;

public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    public string? Password { get; set; }

    public string RequirePassword() =>
        !string.IsNullOrWhiteSpace(Password) && Password.Length >= 8
            ? Password
            : throw new InvalidOperationException(
                "DemoData__Password must contain at least 8 characters. "
                + "Set it in backend/SEF_Project.Api/.env before seeding.");
}

public enum DemoDataCommand
{
    Seed,
    Reset,
    Reseed
}

public sealed record DemoDataResult(bool Changed, string Message);

/// <summary>
/// Creates and removes a development-only, connected demonstration dataset.
/// Every Guid and account email is reserved here so reset removes only records
/// owned by this seeder. The existing migration-owned catalogue is reused.
/// </summary>
public sealed class DemoDataSeeder
{
    public const string CustomerEmail = "customer@demo.clothic";
    public const string StaffEmail = "staff@demo.clothic";
    public const string AdministratorEmail = "admin@demo.clothic";

    private static readonly string[] DemoEmails =
        { CustomerEmail, StaffEmail, AdministratorEmail };

    private static readonly Guid CartId = DemoId(1);
    private static readonly Guid CartItemId = DemoId(2);
    private static readonly Guid WishlistId = DemoId(3);
    private static readonly Guid WishlistItemId = DemoId(4);
    private static readonly Guid ReviewId = DemoId(5);
    private static readonly Guid PurchaseOrderId = DemoId(10);
    private static readonly Guid PurchaseOrderItemId = DemoId(11);
    private static readonly Guid ReceiptTransactionId = DemoId(12);
    private static readonly Guid CampaignId = DemoId(20);
    private static readonly Guid PromotionId = DemoId(21);
    private static readonly Guid CouponId = DemoId(22);
    private static readonly Guid CouponRedemptionId = DemoId(23);
    private static readonly Guid AwaitingWorkflowId = DemoId(30);
    private static readonly Guid AwaitingAnalysisStepId = DemoId(31);
    private static readonly Guid AwaitingApprovalStepId = DemoId(32);
    private static readonly Guid AwaitingProposalExecutionId = DemoId(33);
    private static readonly Guid AwaitingPricingExecutionId = DemoId(34);
    private static readonly Guid AwaitingValidationId = DemoId(35);
    private static readonly Guid AwaitingApprovalId = DemoId(36);
    private static readonly Guid CompletedWorkflowId = DemoId(40);
    private static readonly Guid CompletedStepId = DemoId(41);
    private static readonly Guid CompletedValidationId = DemoId(42);
    private static readonly Guid CompletedApprovalId = DemoId(43);

    private static readonly Guid[] OrderIds =
        { DemoId(100), DemoId(110), DemoId(120), DemoId(130) };
    private static readonly Guid[] OrderItemIds =
        { DemoId(101), DemoId(111), DemoId(121), DemoId(131) };
    private static readonly Guid[] OrderAddressIds =
        { DemoId(102), DemoId(112), DemoId(122), DemoId(132) };
    private static readonly Guid[] PaymentIds =
        { DemoId(103), DemoId(113), DemoId(123), DemoId(133) };
    private static readonly Guid[] ShipmentIds =
        { DemoId(104), DemoId(114), DemoId(124), DemoId(134) };
    private static readonly Guid[] ReturnIds = { DemoId(140) };
    private static readonly Guid[] ReturnItemIds = { DemoId(141) };

    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly TimeProvider _timeProvider;

    public DemoDataSeeder(
        AppDbContext context,
        IPasswordService passwordService,
        TimeProvider timeProvider)
    {
        _context = context;
        _passwordService = passwordService;
        _timeProvider = timeProvider;
    }

    public async Task<DemoDataResult> SeedAsync(
        DemoDataOptions options,
        CancellationToken cancellationToken = default)
    {
        var password = options.RequirePassword();
        var existingEmails = await _context.Users
            .Where(user => DemoEmails.Contains(user.Email))
            .Select(user => user.Email)
            .ToListAsync(cancellationToken);

        if (existingEmails.Count == DemoEmails.Length
            && await HasCompleteDatasetAsync(cancellationToken))
        {
            return new DemoDataResult(
                false,
                "Demo data already exists. Use 'reseed-demo' to recreate it.");
        }

        if (existingEmails.Count > 0
            || await _context.Orders.AnyAsync(order => OrderIds.Contains(order.Id), cancellationToken)
            || await _context.Campaigns.AnyAsync(campaign => campaign.Id == CampaignId, cancellationToken))
        {
            throw new InvalidOperationException(
                "A partial demo dataset or reserved demo identifier already exists. "
                + "Run 'reset-demo' and then 'seed-demo'.");
        }

        await EnsurePrerequisitesAsync(cancellationToken);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var users = CreateUsers(password);
            _context.Users.AddRange(users.CustomerUser, users.StaffUser, users.AdministratorUser);
            await _context.SaveChangesAsync(cancellationToken);

            var customer = new Customer { UserId = users.CustomerUser.Id };
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync(cancellationToken);

            AddCustomerExperience(customer, now);
            await AddOperationsDataAsync(
                customer,
                users.StaffUser,
                users.AdministratorUser,
                now,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new DemoDataResult(
                true,
                "Demo data created for customer, staff and administrator accounts.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> HasCompleteDatasetAsync(
        CancellationToken cancellationToken) =>
        await _context.Orders.CountAsync(
            order => OrderIds.Contains(order.Id),
            cancellationToken) == OrderIds.Length
        && await _context.Campaigns.AnyAsync(
            campaign => campaign.Id == CampaignId,
            cancellationToken)
        && await _context.Promotions.AnyAsync(
            promotion => promotion.Id == PromotionId,
            cancellationToken)
        && await _context.Carts.AnyAsync(
            cart => cart.Id == CartId,
            cancellationToken)
        && await _context.Wishlists.AnyAsync(
            wishlist => wishlist.Id == WishlistId,
            cancellationToken)
        && await _context.AgentWorkflows.CountAsync(
            workflow => workflow.Id == AwaitingWorkflowId
                || workflow.Id == CompletedWorkflowId,
            cancellationToken) == 2;

    public async Task<DemoDataResult> ResetAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var userIds = await _context.Users
                .Where(user => DemoEmails.Contains(user.Email))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
            var customerIds = await _context.Customers
                .Where(customer => userIds.Contains(customer.UserId))
                .Select(customer => customer.Id)
                .ToListAsync(cancellationToken);

            await _context.AgentWorkflows
                .Where(workflow => workflow.Id == AwaitingWorkflowId
                    || workflow.Id == CompletedWorkflowId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.CouponRedemptions
                .Where(redemption => redemption.Id == CouponRedemptionId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Returns
                .Where(productReturn => ReturnIds.Contains(productReturn.Id))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Orders
                .Where(order => OrderIds.Contains(order.Id))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Reviews
                .Where(review => review.Id == ReviewId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.InventoryTransactions
                .Where(stockTransaction => stockTransaction.Id == ReceiptTransactionId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.PurchaseOrders
                .Where(purchaseOrder => purchaseOrder.Id == PurchaseOrderId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Coupons
                .Where(coupon => coupon.Id == CouponId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.PromotionProducts
                .Where(target => target.PromotionId == PromotionId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.PromotionCategories
                .Where(target => target.PromotionId == PromotionId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Promotions
                .Where(promotion => promotion.Id == PromotionId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Campaigns
                .Where(campaign => campaign.Id == CampaignId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.CartItems
                .Where(item => item.Id == CartItemId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Carts
                .Where(cart => cart.Id == CartId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.WishlistItems
                .Where(item => item.Id == WishlistItemId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Wishlists
                .Where(wishlist => wishlist.Id == WishlistId)
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Addresses
                .Where(address => customerIds.Contains(address.CustomerId))
                .ExecuteDeleteAsync(cancellationToken);
            await _context.Customers
                .Where(customer => customerIds.Contains(customer.Id))
                .ExecuteDeleteAsync(cancellationToken);
            var removedUsers = await _context.Users
                .Where(user => DemoEmails.Contains(user.Email))
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            return new DemoDataResult(
                removedUsers > 0,
                removedUsers > 0
                    ? "Demo data removed. Manually created records were preserved."
                    : "No demo data was present.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsurePrerequisitesAsync(CancellationToken cancellationToken)
    {
        var roles = await _context.Roles
            .Select(role => role.Name)
            .ToListAsync(cancellationToken);
        var requiredRoles = new[] { "Customer", "Staff", "Administrator" };
        if (requiredRoles.Except(roles).Any())
        {
            throw new InvalidOperationException(
                "Required roles are missing. Run 'dotnet ef database update' first.");
        }

        var productIds = new[]
        {
            SeedData.ProductTShirt,
            SeedData.ProductJacket,
            SeedData.ProductJeans,
            SeedData.ProductBoots
        };
        if (await _context.Products.CountAsync(
                product => productIds.Contains(product.Id),
                cancellationToken) != productIds.Length)
        {
            throw new InvalidOperationException(
                "The fashion catalogue seed is missing. Apply all EF Core migrations first.");
        }
    }

    private (User CustomerUser, User StaffUser, User AdministratorUser) CreateUsers(
        string password) =>
        (
            User(CustomerEmail, "Demo", "Customer", 1, password),
            User(StaffEmail, "Demo", "Staff", 2, password),
            User(AdministratorEmail, "Demo", "Administrator", 3, password)
        );

    private User User(
        string email,
        string firstName,
        string lastName,
        int roleId,
        string password) => new()
        {
            Email = email,
            PasswordHash = _passwordService.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            RoleId = roleId,
            IsActive = true
        };

    private void AddCustomerExperience(Customer customer, DateTime now)
    {
        customer.Addresses.Add(new Address
        {
            Label = "Home",
            AddressLine1 = "42 Galle Road",
            AddressLine2 = "Apartment 5B",
            City = "Colombo",
            Province = "Western",
            PostalCode = "00300",
            Country = "Sri Lanka",
            IsDefault = true
        });
        customer.Cart = new Cart
        {
            Id = CartId,
            Items = new List<CartItem>
            {
                new()
                {
                    Id = CartItemId,
                    ProductVariantId = SeedData.VariantHoodieM,
                    Quantity = 1
                }
            }
        };
        customer.Wishlist = new Wishlist
        {
            Id = WishlistId,
            Items = new List<WishlistItem>
            {
                new()
                {
                    Id = WishlistItemId,
                    ProductId = SeedData.ProductJacket
                }
            }
        };

        // These graphs use reserved non-empty Guid keys. Add them explicitly;
        // otherwise EF treats a newly attached keyed entity as an update.
        _context.Carts.Add(customer.Cart);
        _context.Wishlists.Add(customer.Wishlist);

        _context.Reviews.Add(new Review
        {
            Id = ReviewId,
            Customer = customer,
            ProductId = SeedData.ProductTShirt,
            Rating = 5,
            Comment = "Soft, well-finished and true to size. A reliable everyday piece.",
            IsPublished = true
        });
    }

    private async Task AddOperationsDataAsync(
        Customer customer,
        User staff,
        User administrator,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var variants = await _context.ProductVariants
            .Where(variant => new[]
            {
                SeedData.VariantJacketL,
                SeedData.VariantTShirtM,
                SeedData.VariantJeansM,
                SeedData.VariantBootsOneSize
            }.Contains(variant.Id))
            .ToDictionaryAsync(variant => variant.Id, cancellationToken);

        AddMarketing(now);
        AddOrders(customer, staff, variants, now);
        await AddInventoryAndPurchasingAsync(staff, variants, now, cancellationToken);
        AddAgentWorkflows(administrator, now);
    }

    private void AddMarketing(DateTime now)
    {
        var campaign = new Campaign
        {
            Id = CampaignId,
            Name = "Demo Seasonal Edit",
            Description = "Active demonstration campaign for the shared Clothic dataset.",
            StartDate = now.Date.AddDays(-7),
            EndDate = now.Date.AddDays(30),
            Status = CampaignStatus.Active
        };
        var promotion = new Promotion
        {
            Id = PromotionId,
            Campaign = campaign,
            Name = "Demo Outerwear 15% Off",
            Description = "An active offer used to demonstrate pricing and campaign analytics.",
            Type = PromotionType.PercentageDiscount,
            DiscountValue = 15m,
            StartDate = campaign.StartDate,
            EndDate = campaign.EndDate,
            IsActive = true,
            PromotionProducts = new List<PromotionProduct>
            {
                new() { ProductId = SeedData.ProductJacket }
            }
        };
        var coupon = new Coupon
        {
            Id = CouponId,
            Promotion = promotion,
            Code = "DEMO15",
            UsageLimit = 100,
            PerCustomerLimit = 5,
            StartsAt = promotion.StartDate,
            EndsAt = promotion.EndDate,
            IsActive = true
        };

        _context.Campaigns.Add(campaign);
        _context.Promotions.Add(promotion);
        _context.Coupons.Add(coupon);
    }

    private void AddOrders(
        Customer customer,
        User staff,
        IReadOnlyDictionary<Guid, ProductVariant> variants,
        DateTime now)
    {
        var completedOffer = CreateOrder(
            0,
            customer,
            variants[SeedData.VariantJacketL],
            OrderStatus.Completed,
            now.AddDays(-3),
            quantity: 1,
            discountPercent: 15m,
            PaymentStatus.Completed,
            ShipmentStatus.Delivered,
            staff.Id);
        _context.CouponRedemptions.Add(new CouponRedemption
        {
            Id = CouponRedemptionId,
            CouponId = CouponId,
            Customer = customer,
            Order = completedOffer,
            RedeemedAt = completedOffer.PlacedAt
        });

        CreateOrder(
            1,
            customer,
            variants[SeedData.VariantTShirtM],
            OrderStatus.Ready,
            now.AddDays(-1),
            quantity: 2,
            discountPercent: 0m,
            PaymentStatus.Pending,
            ShipmentStatus.Shipped,
            staff.Id);

        CreateOrder(
            2,
            customer,
            variants[SeedData.VariantJeansM],
            OrderStatus.Cancelled,
            now.AddDays(-8),
            quantity: 1,
            discountPercent: 0m,
            PaymentStatus.Failed,
            ShipmentStatus.Cancelled,
            staff.Id);

        var returnOrder = CreateOrder(
            3,
            customer,
            variants[SeedData.VariantBootsOneSize],
            OrderStatus.Completed,
            now.AddDays(-14),
            quantity: 1,
            discountPercent: 0m,
            PaymentStatus.Completed,
            ShipmentStatus.Delivered,
            staff.Id);
        var productReturn = new ProductReturn
        {
            Id = ReturnIds[0],
            ReturnNumber = "DEMO-RET-0001",
            Status = ReturnStatus.Requested,
            Reason = ReturnReason.WrongSize,
            CustomerNote = "The fit is smaller than expected.",
            RefundAmount = returnOrder.Items.Single().UnitPrice,
            RequestedAt = now.AddDays(-1),
            Items = new List<ReturnItem>
            {
                new()
                {
                    Id = ReturnItemIds[0],
                    OrderItem = returnOrder.Items.Single(),
                    Quantity = 1,
                    UnitRefundAmount = returnOrder.Items.Single().UnitPrice
                }
            }
        };
        returnOrder.Returns.Add(productReturn);
        _context.Returns.Add(productReturn);
    }

    private Order CreateOrder(
        int index,
        Customer customer,
        ProductVariant variant,
        OrderStatus status,
        DateTime placedAt,
        int quantity,
        decimal discountPercent,
        PaymentStatus paymentStatus,
        ShipmentStatus shipmentStatus,
        int staffUserId)
    {
        var gross = Math.Round(variant.Price * quantity, 2);
        var discount = Math.Round(gross * discountPercent / 100m, 2);
        var total = gross - discount;
        var order = new Order
        {
            Id = OrderIds[index],
            OrderNumber = $"DEMO-ORD-{index + 1:0000}",
            Customer = customer,
            Status = status,
            Subtotal = gross,
            DiscountTotal = discount,
            Total = total,
            Currency = "LKR",
            PlacedAt = placedAt,
            Items = new List<OrderItem>
            {
                new()
                {
                    Id = OrderItemIds[index],
                    ProductVariantId = variant.Id,
                    Quantity = quantity,
                    UnitPrice = Math.Round(total / quantity, 2),
                    LineTotal = total
                }
            },
            DeliveryAddress = new OrderAddress
            {
                Id = OrderAddressIds[index],
                FullName = "Demo Customer",
                Line1 = "42 Galle Road",
                Line2 = "Apartment 5B",
                City = "Colombo",
                Province = "Western",
                PostalCode = "00300",
                Country = "Sri Lanka",
                Phone = "+94 77 555 0101"
            },
            Payments = new List<Payment>
            {
                new()
                {
                    Id = PaymentIds[index],
                    Amount = total,
                    Method = index == 2 ? PaymentMethod.Cash : PaymentMethod.Card,
                    Status = paymentStatus,
                    TransactionReference = paymentStatus == PaymentStatus.Completed
                        ? $"DEMO-PAY-{index + 1:0000}"
                        : null,
                    PaidAt = paymentStatus == PaymentStatus.Completed
                        ? placedAt.AddMinutes(5)
                        : null
                }
            },
            Shipments = new List<Shipment>
            {
                new()
                {
                    Id = ShipmentIds[index],
                    Status = shipmentStatus,
                    Carrier = shipmentStatus == ShipmentStatus.Cancelled ? null : "Clothic Express",
                    TrackingNumber = shipmentStatus == ShipmentStatus.Cancelled
                        ? null
                        : $"DEMO-TRACK-{index + 1:0000}",
                    ShippedAt = shipmentStatus is ShipmentStatus.Shipped or ShipmentStatus.Delivered
                        ? placedAt.AddDays(1)
                        : null,
                    DeliveredAt = shipmentStatus == ShipmentStatus.Delivered
                        ? placedAt.AddDays(2)
                        : null
                }
            }
        };

        var historyStates = status switch
        {
            OrderStatus.Completed => new[]
            {
                OrderStatus.Pending,
                OrderStatus.Confirmed,
                OrderStatus.Preparing,
                OrderStatus.Ready,
                OrderStatus.Completed
            },
            OrderStatus.Ready => new[]
            {
                OrderStatus.Pending,
                OrderStatus.Confirmed,
                OrderStatus.Preparing,
                OrderStatus.Ready
            },
            _ => new[] { OrderStatus.Pending, status }
        };
        for (var historyIndex = 0; historyIndex < historyStates.Length; historyIndex++)
        {
            var historyStatus = historyStates[historyIndex];
            order.StatusHistory.Add(new OrderStatusHistory
            {
                Status = historyStatus,
                ChangedAt = placedAt.AddHours(historyIndex * 6),
                ChangedByUserId = historyIndex == 0 ? null : staffUserId,
                Note = historyIndex == 0
                    ? "Demo order created."
                    : $"Demo order moved to {historyStatus}."
            });
        }

        _context.Orders.Add(order);
        return order;
    }

    private async Task AddInventoryAndPurchasingAsync(
        User staff,
        IReadOnlyDictionary<Guid, ProductVariant> variants,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var supplierId = await _context.Suppliers
            .Where(supplier => supplier.Id == SeedData.SupplierNordicFootwear)
            .Select(supplier => supplier.Id)
            .SingleAsync(cancellationToken);
        var inventory = await _context.Inventory
            .AsNoTracking()
            .SingleAsync(
                stock => stock.ProductVariantId == SeedData.VariantBootsOneSize,
                cancellationToken);

        _context.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = PurchaseOrderId,
            OrderNumber = "DEMO-PO-0001",
            SupplierId = supplierId,
            Status = PurchaseOrderStatus.Received,
            ExpectedAt = now.AddDays(-20),
            SubmittedAt = now.AddDays(-30),
            ReceivedAt = now.AddDays(-22),
            Notes = "Completed demo replenishment order.",
            Items = new List<PurchaseOrderItem>
            {
                new()
                {
                    Id = PurchaseOrderItemId,
                    ProductVariantId = SeedData.VariantBootsOneSize,
                    Quantity = 20,
                    UnitCost = Math.Round(
                        variants[SeedData.VariantBootsOneSize].Price * 0.55m,
                        2)
                }
            }
        });
        var quantityBeforeReceipt = Math.Max(0, inventory.QuantityOnHand - 20);
        _context.InventoryTransactions.Add(new StockTransaction
        {
            Id = ReceiptTransactionId,
            ProductVariantId = SeedData.VariantBootsOneSize,
            Type = InventoryTransactionType.Receipt,
            QuantityChange = inventory.QuantityOnHand - quantityBeforeReceipt,
            QuantityOnHandBefore = quantityBeforeReceipt,
            QuantityOnHandAfter = inventory.QuantityOnHand,
            PerformedByUser = staff,
            Reference = "DEMO-PO-0001",
            Note = "Demo purchase-order receipt; current stock already includes this quantity."
        });
    }

    private void AddAgentWorkflows(User administrator, DateTime now)
    {
        var proposal = new PromotionProposalDocument
        {
            SchemaVersion = PromotionAgentConstants.SchemaVersion,
            Summary = "One slow-moving product is suitable for a bounded promotion.",
            Proposals = new List<PromotionProposalItem>
            {
                new()
                {
                    ProductId = SeedData.ProductJacket,
                    ProductName = "Everyday Jacket",
                    PromotionType = "PercentageDiscount",
                    DiscountValue = 15m,
                    StartDate = now.Date.AddDays(1),
                    EndDate = now.Date.AddDays(15),
                    Rationale = "Sales slowed while available stock remains above the reorder level.",
                    Evidence = new ProposalEvidence
                    {
                        UnitsSold = 2,
                        PreviousUnitsSold = 6,
                        AvailableQuantity = 18
                    }
                }
            }
        };
        var plan = JsonSerializer.Serialize(new
        {
            agent = PromotionAgentConstants.AgentName,
            plan = new[]
            {
                "Retrieve sales and inventory evidence",
                "Draft and validate a bounded promotion",
                "Pause for human approval"
            },
            input = new
            {
                focus = "SlowMoving",
                analysisDays = 30,
                maxProposals = 3,
                maxDiscountPercent = 20,
                excludeProductIds = Array.Empty<Guid>()
            }
        }, AgentJson.Options);
        var pricing = JsonSerializer.Serialize(new ProposalPricingResponse
        {
            ProductId = SeedData.ProductJacket,
            Variants = new List<VariantPricingResponse>
            {
                new()
                {
                    ProductVariantId = SeedData.VariantJacketL,
                    Sku = "JKT-EVR-L",
                    OriginalPrice = 12500m,
                    DiscountAmount = 1875m,
                    FinalPrice = 10625m
                }
            }
        }, AgentJson.Options);

        var analysisStep = new AgentWorkflowStep
        {
            Id = AwaitingAnalysisStepId,
            StepOrder = 1,
            AgentName = PromotionAgentConstants.AgentName,
            Title = "Analyse slow-moving inventory",
            Status = AgentStepStatus.Completed,
            Summary = "Found one grounded promotion candidate.",
            StartedAt = now.AddMinutes(-8),
            CompletedAt = now.AddMinutes(-6),
            ToolExecutions = new List<AgentToolExecution>
            {
                new()
                {
                    Id = AwaitingProposalExecutionId,
                    ToolName = PromotionAgentConstants.SubmitProposal,
                    ToolArgumentsJson = JsonSerializer.Serialize(proposal, AgentJson.Options),
                    Status = AgentToolStatus.Success,
                    StartedAt = now.AddMinutes(-7),
                    CompletedAt = now.AddMinutes(-7)
                },
                new()
                {
                    Id = AwaitingPricingExecutionId,
                    ToolName = PromotionAgentConstants.CalculatePromotion,
                    ToolArgumentsJson = "{\"source\":\"demo\"}",
                    ToolResultJson = pricing,
                    Status = AgentToolStatus.Success,
                    StartedAt = now.AddMinutes(-7),
                    CompletedAt = now.AddMinutes(-7)
                }
            },
            ValidationResults = new List<AgentValidationResult>
            {
                new()
                {
                    Id = AwaitingValidationId,
                    ValidatorName = PromotionAgentConstants.ValidatorName,
                    IsValid = true,
                    Severity = ValidationSeverity.Info,
                    Message = "Product, stock, price, date and discount checks passed."
                }
            }
        };
        var approvalStep = new AgentWorkflowStep
        {
            Id = AwaitingApprovalStepId,
            StepOrder = 2,
            AgentName = PromotionAgentConstants.ReviewerName,
            Title = "Human approval required (low impact)",
            Status = AgentStepStatus.Pending,
            Summary = "One promotion is awaiting review.",
            StartedAt = now.AddMinutes(-5)
        };
        _context.AgentWorkflows.Add(new AgentWorkflow
        {
            Id = AwaitingWorkflowId,
            Objective = "Find a safe promotion for slow-moving outerwear.",
            Status = AgentWorkflowStatus.AwaitingApproval,
            PlanSummary = plan,
            StartedAt = now.AddMinutes(-10),
            Steps = new List<AgentWorkflowStep> { analysisStep, approvalStep },
            Approvals = new List<AgentApproval>
            {
                new()
                {
                    Id = AwaitingApprovalId,
                    Step = approvalStep,
                    Status = ApprovalStatus.Pending,
                    RequestedAt = now.AddMinutes(-5)
                }
            }
        });

        var completedStep = new AgentWorkflowStep
        {
            Id = CompletedStepId,
            StepOrder = 1,
            AgentName = PromotionAgentConstants.ValidatorName,
            Title = "Validate promotion inventory",
            Status = AgentStepStatus.Completed,
            Summary = "No additional products required a promotion.",
            StartedAt = now.AddDays(-2).AddMinutes(-3),
            CompletedAt = now.AddDays(-2).AddMinutes(-1),
            ValidationResults = new List<AgentValidationResult>
            {
                new()
                {
                    Id = CompletedValidationId,
                    ValidatorName = PromotionAgentConstants.ValidatorName,
                    IsValid = true,
                    Severity = ValidationSeverity.Info,
                    Message = "All deterministic checks passed."
                }
            }
        };
        _context.AgentWorkflows.Add(new AgentWorkflow
        {
            Id = CompletedWorkflowId,
            Objective = "Review products with declining sales without duplicating live offers.",
            Status = AgentWorkflowStatus.Completed,
            PlanSummary = plan,
            FinalOutcome = "No additional promotion was required; nothing was changed.",
            StartedAt = now.AddDays(-2).AddMinutes(-5),
            CompletedAt = now.AddDays(-2),
            Steps = new List<AgentWorkflowStep> { completedStep },
            Approvals = new List<AgentApproval>
            {
                new()
                {
                    Id = CompletedApprovalId,
                    Status = ApprovalStatus.Approved,
                    RequestedAt = now.AddDays(-2).AddMinutes(-2),
                    ReviewedAt = now.AddDays(-2).AddMinutes(-1),
                    ReviewedByUser = administrator,
                    Comment = "Demo approval completed after validation."
                }
            }
        });
    }

    private static Guid DemoId(int value) =>
        Guid.Parse($"90000000-0000-0000-0000-{value:000000000000}");
}
