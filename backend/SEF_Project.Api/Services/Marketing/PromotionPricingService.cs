using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Services.Marketing;

public class PromotionPricingService : IPromotionPricingService
{
    private const string Currency = "LKR";

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PromotionPricingService> _logger;

    public PromotionPricingService(
        AppDbContext context,
        TimeProvider timeProvider,
        ILogger<PromotionPricingService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PromotionDiscountResponse?> CalculatePromotionDiscountAsync(
        Guid promotionId,
        CalculatePromotionDiscountRequest request,
        CancellationToken cancellationToken = default)
    {
        var promotion = await _context.Promotions
            .AsNoTracking()
            .Include(p => p.Campaign)
            .Include(p => p.PromotionProducts)
            .Include(p => p.PromotionCategories)
            .FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken);

        var variant = await _context.ProductVariants
            .AsNoTracking()
            .Include(v => v.Product)
                .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId, cancellationToken);

        if (promotion is null || variant is null)
        {
            return null;
        }

        if (!variant.IsActive || !variant.Product.IsActive)
        {
            throw new InvalidOperationException(
                $"Product variant '{variant.Sku}' is not available.");
        }

        if (promotion.Campaign is not null
            && promotion.Campaign.Status != CampaignStatus.Active)
        {
            throw new InvalidOperationException(
                $"The promotion's campaign is not active ('{promotion.Campaign.Status}').");
        }

        // A promotion applies when it targets the variant's product directly
        // or one of the product's categories. Untargeted promotions apply to nothing.
        var targetsProduct = promotion.PromotionProducts
            .Any(pp => pp.ProductId == variant.ProductId);
        var productCategoryIds = new List<Guid> { variant.Product.CategoryId };
        var targetsCategory = promotion.PromotionCategories
            .Any(pc => productCategoryIds.Contains(pc.CategoryId));

        if (!targetsProduct && !targetsCategory)
        {
            throw new InvalidOperationException(
                $"Promotion does not apply to product variant '{variant.Sku}'.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var result = PromotionDiscountCalculator.Calculate(
            promotion,
            variant.Price,
            now);

        _logger.LogInformation(
            "Promotion {PromotionId} applied to {Sku}: {OriginalPrice} - {DiscountAmount} = {FinalPrice}.",
            promotion.Id,
            variant.Sku,
            result.OriginalPrice,
            result.DiscountAmount,
            result.FinalPrice);

        return new PromotionDiscountResponse
        {
            PromotionId = promotion.Id,
            PromotionName = promotion.Name,
            PromotionType = promotion.Type,
            ProductId = variant.ProductId,
            ProductVariantId = variant.Id,
            Sku = variant.Sku,
            OriginalPrice = result.OriginalPrice,
            DiscountAmount = result.DiscountAmount,
            FinalPrice = result.FinalPrice,
            Currency = Currency,
            CalculatedAt = now
        };
    }
}
