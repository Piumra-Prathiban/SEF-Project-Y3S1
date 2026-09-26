using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Analytics;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Orders;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Services.Analytics;

/// <remarks>
/// Aggregation is done in the database. Money is summed as double precision
/// (EF Core's SQLite provider cannot SUM decimals) and rounded to 2 decimal
/// places, which is exact for totals below ~10^13.
/// </remarks>
public class AnalyticsService : IAnalyticsService
{
    /// <summary>Order statuses counted as sales (placed, not cancelled/refunded, confirmed).</summary>
    public static readonly OrderStatus[] SalesStatuses =
    {
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.Ready,
        OrderStatus.Completed
    };

    private const string Currency = "LKR";
    private const int MaxDailyPoints = 366;
    private const decimal TrendThresholdPercent = 10m;
    private static readonly TimeSpan DefaultRange = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxRange = TimeSpan.FromDays(3 * 366);

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AnalyticsService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    // ---- Sales -----------------------------------------------------------

    public async Task<SalesSummaryResponse> GetSalesSummaryAsync(
        AnalyticsDateRangeQuery query,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(query);

        var totals = await SaleOrders(from, to)
            .GroupBy(o => 1)
            .Select(g => new
            {
                OrderCount = g.Count(),
                Gross = g.Sum(o => (double)o.Subtotal),
                Discount = g.Sum(o => (double)o.DiscountTotal),
                Net = g.Sum(o => (double)o.Total)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var unitsSold = await SaleItems(from, to)
            .SumAsync(i => (int?)i.Quantity, cancellationToken) ?? 0;

        var orderCount = totals?.OrderCount ?? 0;
        var net = Money(totals?.Net ?? 0);

        return new SalesSummaryResponse
        {
            From = from,
            To = to,
            OrderCount = orderCount,
            UnitsSold = unitsSold,
            GrossSales = Money(totals?.Gross ?? 0),
            DiscountTotal = Money(totals?.Discount ?? 0),
            NetRevenue = net,
            AverageOrderValue = orderCount == 0
                ? 0m
                : Math.Round(net / orderCount, 2, MidpointRounding.AwayFromZero),
            Currency = Currency,
            IncludedStatuses = SalesStatuses.ToList()
        };
    }

    public async Task<SalesOverTimeResponse> GetSalesOverTimeAsync(
        SalesOverTimeQuery query,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(query);
        var periods = BuildPeriods(from, to, query.Granularity);

        // Grouped by UTC calendar day in the database; days are folded into
        // months in memory (at most a few hundred rows).
        var orderDays = await SaleOrders(from, to)
            .GroupBy(o => new { o.PlacedAt.Year, o.PlacedAt.Month, o.PlacedAt.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                OrderCount = g.Count(),
                Net = g.Sum(o => (double)o.Total)
            })
            .ToListAsync(cancellationToken);

        var unitDays = await SaleItems(from, to)
            .GroupBy(i => new { i.Order.PlacedAt.Year, i.Order.PlacedAt.Month, i.Order.PlacedAt.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Units = g.Sum(i => i.Quantity)
            })
            .ToListAsync(cancellationToken);

        DateTime PeriodOf(int year, int month, int day) =>
            query.Granularity == TimeGranularity.Month
                ? new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc)
                : new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);

        var orderTotals = orderDays
            .GroupBy(d => PeriodOf(d.Year, d.Month, d.Day))
            .ToDictionary(
                g => g.Key,
                g => (Count: g.Sum(d => d.OrderCount), Net: g.Sum(d => d.Net)));

        var unitTotals = unitDays
            .GroupBy(d => PeriodOf(d.Year, d.Month, d.Day))
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Units));

        return new SalesOverTimeResponse
        {
            From = from,
            To = to,
            Granularity = query.Granularity,
            Points = periods.Select(period => new SalesTimePoint
            {
                PeriodStart = period,
                OrderCount = orderTotals.TryGetValue(period, out var o) ? o.Count : 0,
                NetRevenue = orderTotals.TryGetValue(period, out var n) ? Money(n.Net) : 0m,
                UnitsSold = unitTotals.GetValueOrDefault(period)
            }).ToList()
        };
    }

    // ---- Product performance ----------------------------------------------

    public async Task<AnalyticsPagedResponse<ProductPerformanceItem>> GetProductPerformanceAsync(
        ProductPerformanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(query);
        var items = SaleItems(from, to);

        var products = _context.Products.AsNoTracking();

        if (!query.IncludeInactive)
        {
            products = products.Where(p => p.IsActive);
        }

        // Sales are aggregated once per product (a single GROUP BY) rather than
        // with correlated subqueries per product row, which re-scanned the
        // order items for every product and again for the sort key.
        var sales = items
            .GroupBy(i => i.ProductVariant.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                UnitsSold = g.Sum(i => i.Quantity),
                OrderCount = g.Select(i => i.OrderId).Distinct().Count(),
                Revenue = g.Sum(i => (double)i.LineTotal)
            });

        // Starts from Products so products with no sales are included
        // (needed for low performers).
        var rows =
            from p in products
            join s in sales on p.Id equals s.ProductId into productSales
            from s in productSales.DefaultIfEmpty()
            select new ProductRow
            {
                ProductId = p.Id,
                ProductName = p.Name,
                IsActive = p.IsActive,
                UnitsSold = (int?)s!.UnitsSold ?? 0,
                OrderCount = (int?)s!.OrderCount ?? 0,
                Revenue = (double?)s!.Revenue ?? 0
            };

        var descending = IsDescending(query.SortDirection, defaultDescending: true);

        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "revenue" => OrderBy(rows, r => r.Revenue, descending),
            "ordercount" => OrderBy(rows, r => r.OrderCount, descending),
            "name" => OrderBy(rows, r => r.ProductName, descending),
            _ => OrderBy(rows, r => r.UnitsSold, descending)
        };

        rows = ordered
            .ThenBy(r => r.ProductName)
            .ThenBy(r => r.ProductId);

        var (page, pageSize, totalCount, pageRows) =
            await PageAsync(rows, query.Page, query.PageSize, cancellationToken);

        return new AnalyticsPagedResponse<ProductPerformanceItem>
        {
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = pageRows.Select(r => new ProductPerformanceItem
            {
                ProductId = r.ProductId,
                ProductName = r.ProductName,
                IsActive = r.IsActive,
                UnitsSold = r.UnitsSold,
                OrderCount = r.OrderCount,
                Revenue = Money(r.Revenue)
            }).ToList()
        };
    }

    // ---- Inventory ---------------------------------------------------------

    public async Task<PagedResponse<InventoryStockItem>> GetInventoryStockAsync(
        InventoryStockQuery query,
        CancellationToken cancellationToken = default)
    {
        var rows = StockRows(query.IncludeInactive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            rows = rows.Where(r =>
                r.Sku.ToLower().Contains(search)
                || r.ProductName.ToLower().Contains(search));
        }

        rows = query.StockStatus switch
        {
            StockStatus.OutOfStock => rows.Where(r =>
                r.QuantityOnHand - r.ReservedQuantity <= 0),
            StockStatus.LowStock => rows.Where(r =>
                r.QuantityOnHand - r.ReservedQuantity > 0
                && r.QuantityOnHand - r.ReservedQuantity <= r.ReorderLevel),
            StockStatus.InStock => rows.Where(r =>
                r.QuantityOnHand - r.ReservedQuantity > r.ReorderLevel),
            _ => rows
        };

        var descending = IsDescending(query.SortDirection, defaultDescending: false);

        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "quantityonhand" => OrderBy(rows, r => r.QuantityOnHand, descending),
            "sku" => OrderBy(rows, r => r.Sku, descending),
            "productname" => OrderBy(rows, r => r.ProductName, descending),
            _ => OrderBy(rows, r => r.QuantityOnHand - r.ReservedQuantity, descending)
        };

        rows = ordered.ThenBy(r => r.Sku);

        var (page, pageSize, totalCount, pageRows) =
            await PageAsync(rows, query.Page, query.PageSize, cancellationToken);

        return new PagedResponse<InventoryStockItem>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = pageRows.Select(r =>
            {
                var available = r.QuantityOnHand - r.ReservedQuantity;

                return new InventoryStockItem
                {
                    ProductVariantId = r.ProductVariantId,
                    ProductId = r.ProductId,
                    ProductName = r.ProductName,
                    Sku = r.Sku,
                    VariantName = r.VariantName,
                    IsActive = r.IsActive,
                    HasInventoryRecord = r.HasInventoryRecord,
                    QuantityOnHand = r.QuantityOnHand,
                    ReservedQuantity = r.ReservedQuantity,
                    AvailableQuantity = available,
                    ReorderLevel = r.ReorderLevel,
                    StockStatus = ClassifyStock(available, r.ReorderLevel)
                };
            }).ToList()
        };
    }

    public async Task<InventorySummaryResponse> GetInventorySummaryAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var summary = await StockRows(includeInactive)
            .GroupBy(r => 1)
            .Select(g => new
            {
                VariantCount = g.Count(),
                OutOfStock = g.Sum(r =>
                    r.QuantityOnHand - r.ReservedQuantity <= 0 ? 1 : 0),
                LowStock = g.Sum(r =>
                    r.QuantityOnHand - r.ReservedQuantity > 0
                    && r.QuantityOnHand - r.ReservedQuantity <= r.ReorderLevel ? 1 : 0),
                OnHand = g.Sum(r => r.QuantityOnHand),
                Reserved = g.Sum(r => r.ReservedQuantity)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return new InventorySummaryResponse();
        }

        return new InventorySummaryResponse
        {
            VariantCount = summary.VariantCount,
            OutOfStockCount = summary.OutOfStock,
            LowStockCount = summary.LowStock,
            InStockCount = summary.VariantCount - summary.OutOfStock - summary.LowStock,
            TotalQuantityOnHand = summary.OnHand,
            TotalReservedQuantity = summary.Reserved
        };
    }

    // ---- Promotion performance --------------------------------------------

    public async Task<PromotionPerformanceResponse> GetPromotionPerformanceAsync(
        PromotionPerformanceQuery query,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(query);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var redemptions = _context.CouponRedemptions
            .AsNoTracking()
            .Where(r => r.RedeemedAt >= from && r.RedeemedAt < to);

        var saleStatusOrders = _context.Orders
            .AsNoTracking()
            .Where(IsSale);

        var rows = _context.Promotions.AsNoTracking().Select(p => new PromotionRow
        {
            PromotionId = p.Id,
            Name = p.Name,
            Type = p.Type,
            DiscountValue = (double)p.DiscountValue,
            CampaignName = p.Campaign == null ? null : p.Campaign.Name,
            IsLive = p.IsActive
                && p.StartDate <= now
                && p.EndDate >= now
                && (p.Campaign == null || p.Campaign.Status == CampaignStatus.Active),
            CouponCount = p.Coupons.Count,
            Redemptions = redemptions.Count(r => r.Coupon.PromotionId == p.Id),
            UniqueCustomers = redemptions
                .Where(r => r.Coupon.PromotionId == p.Id)
                .Select(r => r.CustomerId)
                .Distinct()
                .Count(),
            RedeemedOrderCount = saleStatusOrders.Count(o =>
                redemptions.Any(r => r.OrderId == o.Id && r.Coupon.PromotionId == p.Id)),
            RedeemedOrderRevenue = saleStatusOrders
                .Where(o => redemptions.Any(r => r.OrderId == o.Id && r.Coupon.PromotionId == p.Id))
                .Sum(o => (double?)o.Total) ?? 0,
            DiscountAmount = saleStatusOrders
                .Where(o => redemptions.Any(r => r.OrderId == o.Id && r.Coupon.PromotionId == p.Id))
                .Sum(o => (double?)o.DiscountTotal) ?? 0
        });

        var livePromotionCount = await rows.CountAsync(r => r.IsLive, cancellationToken);
        var totalRedemptions = await redemptions.CountAsync(cancellationToken);

        if (query.LiveOnly)
        {
            rows = rows.Where(r => r.IsLive);
        }

        var descending = IsDescending(query.SortDirection, defaultDescending: true);

        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "revenue" => OrderBy(rows, r => r.RedeemedOrderRevenue, descending),
            "discountamount" => OrderBy(rows, r => r.DiscountAmount, descending),
            "name" => OrderBy(rows, r => r.Name, descending),
            _ => OrderBy(rows, r => r.Redemptions, descending)
        };

        rows = ordered
            .ThenBy(r => r.Name)
            .ThenBy(r => r.PromotionId);

        var (page, pageSize, totalCount, pageRows) =
            await PageAsync(rows, query.Page, query.PageSize, cancellationToken);

        return new PromotionPerformanceResponse
        {
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            LivePromotionCount = livePromotionCount,
            TotalRedemptions = totalRedemptions,
            Items = pageRows.Select(r => new PromotionPerformanceItem
            {
                PromotionId = r.PromotionId,
                Name = r.Name,
                Type = r.Type,
                DiscountValue = Money(r.DiscountValue),
                CampaignName = r.CampaignName,
                IsLive = r.IsLive,
                CouponCount = r.CouponCount,
                Redemptions = r.Redemptions,
                UniqueCustomers = r.UniqueCustomers,
                RedeemedOrderCount = r.RedeemedOrderCount,
                RedeemedOrderRevenue = Money(r.RedeemedOrderRevenue),
                DiscountAmount = Money(r.DiscountAmount)
            }).ToList()
        };
    }

    // ---- Demand insights ---------------------------------------------------

    public async Task<AnalyticsPagedResponse<DemandItem>> GetDemandInsightsAsync(
        DemandQuery query,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(query);
        var span = to - from;

        var variants = _context.ProductVariants.AsNoTracking();

        if (!query.IncludeInactive)
        {
            variants = variants.Where(v => v.IsActive && v.Product.IsActive);
        }

        // Each window is aggregated once per variant (a plain GROUP BY) and
        // joined to the variants. Correlated per-variant subqueries, or a
        // conditional sum across both windows, made EF re-scan the order
        // items for every variant.
        var current = SaleItems(from, to)
            .GroupBy(i => i.ProductVariantId)
            .Select(g => new { ProductVariantId = g.Key, UnitsSold = g.Sum(i => i.Quantity) });

        var previous = SaleItems(from - span, from)
            .GroupBy(i => i.ProductVariantId)
            .Select(g => new { ProductVariantId = g.Key, UnitsSold = g.Sum(i => i.Quantity) });

        var rows =
            from v in variants
            join c in current on v.Id equals c.ProductVariantId into currentSales
            from c in currentSales.DefaultIfEmpty()
            join p in previous on v.Id equals p.ProductVariantId into previousSales
            from p in previousSales.DefaultIfEmpty()
            select new DemandRow
            {
                ProductVariantId = v.Id,
                ProductId = v.ProductId,
                ProductName = v.Product.Name,
                Sku = v.Sku,
                UnitsSold = (int?)c!.UnitsSold ?? 0,
                PreviousUnitsSold = (int?)p!.UnitsSold ?? 0,
                AvailableQuantity = v.Inventory == null
                    ? 0
                    : v.Inventory.QuantityOnHand - v.Inventory.ReservedQuantity
            };

        var descending = IsDescending(query.SortDirection, defaultDescending: true);

        Expression<Func<DemandRow, double>> trendKey = r => r.PreviousUnitsSold == 0
            ? 0.0
            : (double)(r.UnitsSold - r.PreviousUnitsSold) / r.PreviousUnitsSold;

        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            // Variants without a previous-window baseline sort last either way.
            "trend" => descending
                ? rows.OrderBy(r => r.PreviousUnitsSold == 0 ? 1 : 0).ThenByDescending(trendKey)
                : rows.OrderBy(r => r.PreviousUnitsSold == 0 ? 1 : 0).ThenBy(trendKey),
            "availablequantity" => OrderBy(rows, r => r.AvailableQuantity, descending),
            "sku" => OrderBy(rows, r => r.Sku, descending),
            _ => OrderBy(rows, r => r.UnitsSold, descending)
        };

        rows = ordered.ThenBy(r => r.Sku);

        var (page, pageSize, totalCount, pageRows) =
            await PageAsync(rows, query.Page, query.PageSize, cancellationToken);

        var days = (decimal)span.TotalDays;

        return new AnalyticsPagedResponse<DemandItem>
        {
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = pageRows.Select(r => ToDemandItem(r, days)).ToList()
        };
    }

    // ---- Shared helpers -----------------------------------------------------

    private static readonly Expression<Func<Order, bool>> IsSale = o =>
        o.Status == OrderStatus.Confirmed
        || o.Status == OrderStatus.Preparing
        || o.Status == OrderStatus.Ready
        || o.Status == OrderStatus.Completed;

    private IQueryable<Order> SaleOrders(DateTime from, DateTime to) =>
        _context.Orders
            .AsNoTracking()
            .Where(IsSale)
            .Where(o => o.PlacedAt >= from && o.PlacedAt < to);

    private IQueryable<OrderItem> SaleItems(DateTime from, DateTime to) =>
        _context.OrderItems
            .AsNoTracking()
            .Where(i =>
                (i.Order.Status == OrderStatus.Confirmed
                 || i.Order.Status == OrderStatus.Preparing
                 || i.Order.Status == OrderStatus.Ready
                 || i.Order.Status == OrderStatus.Completed)
                && i.Order.PlacedAt >= from
                && i.Order.PlacedAt < to);

    private IQueryable<StockRow> StockRows(bool includeInactive)
    {
        var variants = _context.ProductVariants.AsNoTracking();

        if (!includeInactive)
        {
            variants = variants.Where(v => v.IsActive && v.Product.IsActive);
        }

        // Variants without an Inventory row count as zero stock.
        return variants.Select(v => new StockRow
        {
            ProductVariantId = v.Id,
            ProductId = v.ProductId,
            ProductName = v.Product.Name,
            Sku = v.Sku,
            VariantName = v.Name,
            IsActive = v.IsActive && v.Product.IsActive,
            HasInventoryRecord = v.Inventory != null,
            QuantityOnHand = v.Inventory == null ? 0 : v.Inventory.QuantityOnHand,
            ReservedQuantity = v.Inventory == null ? 0 : v.Inventory.ReservedQuantity,
            ReorderLevel = v.Inventory == null ? 0 : v.Inventory.ReorderLevel
        });
    }

    private (DateTime From, DateTime To) ResolveRange(AnalyticsDateRangeQuery query)
    {
        var to = query.To is null
            ? _timeProvider.GetUtcNow().UtcDateTime
            : MarketingDates.ToUtc(query.To.Value);
        var from = query.From is null
            ? to - DefaultRange
            : MarketingDates.ToUtc(query.From.Value);

        if (to <= from)
        {
            throw new ArgumentException("'to' must be after 'from'.");
        }

        if (to - from > MaxRange)
        {
            throw new ArgumentException("The date range cannot exceed 3 years.");
        }

        return (from, to);
    }

    private static List<DateTime> BuildPeriods(
        DateTime from,
        DateTime to,
        TimeGranularity granularity)
    {
        var periods = new List<DateTime>();

        if (granularity == TimeGranularity.Month)
        {
            for (var p = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                 p < to;
                 p = p.AddMonths(1))
            {
                periods.Add(p);
            }

            return periods;
        }

        for (var p = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc); p < to; p = p.AddDays(1))
        {
            if (periods.Count == MaxDailyPoints)
            {
                throw new ArgumentException(
                    $"Daily granularity supports at most {MaxDailyPoints} days; use Month.");
            }

            periods.Add(p);
        }

        return periods;
    }

    private static DemandItem ToDemandItem(DemandRow r, decimal days)
    {
        var unitsPerDay = r.UnitsSold / days;

        decimal? trendPercent = r.PreviousUnitsSold == 0
            ? null
            : Math.Round(
                (r.UnitsSold - r.PreviousUnitsSold) * 100m / r.PreviousUnitsSold,
                1,
                MidpointRounding.AwayFromZero);

        var trend = (r.UnitsSold, r.PreviousUnitsSold, trendPercent) switch
        {
            (0, 0, _) => DemandTrend.NoSales,
            (_, 0, _) => DemandTrend.New,
            (_, _, >= TrendThresholdPercent) => DemandTrend.Rising,
            (_, _, <= -TrendThresholdPercent) => DemandTrend.Falling,
            _ => DemandTrend.Stable
        };

        decimal? daysOfCover = r.UnitsSold == 0
            ? null
            : Math.Round(
                Math.Max(r.AvailableQuantity, 0) / unitsPerDay,
                1,
                MidpointRounding.AwayFromZero);

        return new DemandItem
        {
            ProductVariantId = r.ProductVariantId,
            ProductId = r.ProductId,
            ProductName = r.ProductName,
            Sku = r.Sku,
            UnitsSold = r.UnitsSold,
            PreviousUnitsSold = r.PreviousUnitsSold,
            UnitsPerDay = Math.Round(unitsPerDay, 2, MidpointRounding.AwayFromZero),
            TrendPercent = trendPercent,
            Trend = trend,
            AvailableQuantity = r.AvailableQuantity,
            DaysOfCover = daysOfCover
        };
    }

    public static StockStatus ClassifyStock(int available, int reorderLevel) =>
        available <= 0
            ? StockStatus.OutOfStock
            : available <= reorderLevel
                ? StockStatus.LowStock
                : StockStatus.InStock;

    private static decimal Money(double value) =>
        Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);

    private static bool IsDescending(string? sortDirection, bool defaultDescending) =>
        string.IsNullOrWhiteSpace(sortDirection)
            ? defaultDescending
            : !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

    private static IOrderedQueryable<T> OrderBy<T, TKey>(
        IQueryable<T> source,
        Expression<Func<T, TKey>> key,
        bool descending) =>
        descending ? source.OrderByDescending(key) : source.OrderBy(key);

    private static async Task<(int Page, int PageSize, int TotalCount, List<T> Items)> PageAsync<T>(
        IQueryable<T> source,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Clamp(page, 1, PagingLimits.MaxPage);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (page, pageSize, totalCount, items);
    }

    // Projection rows (translated to SQL; mapped to DTOs after paging).

    private sealed class ProductRow
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int UnitsSold { get; set; }
        public int OrderCount { get; set; }
        public double Revenue { get; set; }
    }

    private sealed class StockRow
    {
        public Guid ProductVariantId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string VariantName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool HasInventoryRecord { get; set; }
        public int QuantityOnHand { get; set; }
        public int ReservedQuantity { get; set; }
        public int ReorderLevel { get; set; }
    }

    private sealed class PromotionRow
    {
        public Guid PromotionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public PromotionType Type { get; set; }
        public double DiscountValue { get; set; }
        public string? CampaignName { get; set; }
        public bool IsLive { get; set; }
        public int CouponCount { get; set; }
        public int Redemptions { get; set; }
        public int UniqueCustomers { get; set; }
        public int RedeemedOrderCount { get; set; }
        public double RedeemedOrderRevenue { get; set; }
        public double DiscountAmount { get; set; }
    }

    private sealed class DemandRow
    {
        public Guid ProductVariantId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public int PreviousUnitsSold { get; set; }
        public int AvailableQuantity { get; set; }
    }
}
