using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Services.Marketing;

public class PromotionOfferService : IPromotionOfferService
{
    private const string Currency = "LKR";

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public PromotionOfferService(AppDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<PromotionProductsResponse?> GetPromotionProductsAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var promotion = await LivePromotions(now)
            .Include(p => p.PromotionProducts)
            .Include(p => p.PromotionCategories)
            .FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken);

        if (promotion is null)
        {
            return null;
        }

        var productIds = promotion.PromotionProducts.Select(pp => pp.ProductId).ToList();
        var categoryIds = promotion.PromotionCategories.Select(pc => pc.CategoryId).ToList();

        var products = await ActiveProducts()
            .Where(p =>
                productIds.Contains(p.Id)
                || categoryIds.Contains(p.CategoryId))
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return new PromotionProductsResponse
        {
            PromotionId = promotion.Id,
            PromotionName = promotion.Name,
            HasPriceDiscount = IsPriceDiscount(promotion.Type),
            Products = products.Select(product => new ProductOfferResponse
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Description = product.Description,
                Variants = product.Variants
                    .OrderBy(v => v.Price)
                    .Select(v => PriceVariant(v, new[] { promotion }, now))
                    .ToList()
            }).ToList()
        };
    }

    public async Task<ProductPromotionsResponse?> GetProductPromotionsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var product = await ActiveProducts()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return null;
        }

        var categoryIds = new List<Guid> { product.CategoryId };

        // Deterministic order: soonest-ending first, then id (used for ties).
        var promotions = await LivePromotions(now)
            .Where(p =>
                p.PromotionProducts.Any(pp => pp.ProductId == productId)
                || p.PromotionCategories.Any(pc => categoryIds.Contains(pc.CategoryId)))
            .OrderBy(p => p.EndDate)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        return new ProductPromotionsResponse
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Description = product.Description,
            HasActivePromotion = promotions.Count > 0,
            Promotions = promotions.Select(p => new PromotionSummaryResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Type = p.Type,
                DiscountValue = p.DiscountValue,
                StartDate = p.StartDate,
                EndDate = p.EndDate
            }).ToList(),
            Variants = product.Variants
                .OrderBy(v => v.Price)
                .Select(v => PriceVariant(v, promotions, now))
                .ToList()
        };
    }

    private IQueryable<Promotion> LivePromotions(DateTime now) =>
        _context.Promotions
            .AsNoTracking()
            .Where(p =>
                p.IsActive
                && p.StartDate <= now
                && p.EndDate >= now
                && (p.Campaign == null || p.Campaign.Status == CampaignStatus.Active));

    private IQueryable<Product> ActiveProducts() =>
        _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants.Where(v => v.IsActive))
            .Where(p => p.IsActive);

    // Prices a variant with the promotion giving the largest discount; the
    // first promotion in the given order wins ties.
    private static VariantOfferResponse PriceVariant(
        ProductVariant variant,
        IEnumerable<Promotion> promotions,
        DateTime now)
    {
        var offer = new VariantOfferResponse
        {
            ProductVariantId = variant.Id,
            Sku = variant.Sku,
            Name = variant.Name,
            OriginalPrice = variant.Price,
            DiscountAmount = 0m,
            FinalPrice = variant.Price,
            Currency = Currency
        };

        foreach (var promotion in promotions.Where(p => IsPriceDiscount(p.Type)))
        {
            var discount = PromotionDiscountCalculator.Calculate(promotion, variant.Price, now);

            if (discount.DiscountAmount > offer.DiscountAmount)
            {
                offer.OriginalPrice = discount.OriginalPrice;
                offer.DiscountAmount = discount.DiscountAmount;
                offer.FinalPrice = discount.FinalPrice;
                offer.PromotionId = promotion.Id;
                offer.PromotionName = promotion.Name;
            }
        }

        return offer;
    }

    private static bool IsPriceDiscount(PromotionType type) =>
        type is PromotionType.PercentageDiscount or PromotionType.FixedAmountDiscount;
}
