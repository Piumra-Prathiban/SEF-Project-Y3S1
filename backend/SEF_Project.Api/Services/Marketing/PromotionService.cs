using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Services.Marketing;

public class PromotionService : IPromotionService
{
    private static readonly CampaignStatus[] ClosedCampaignStatuses =
        { CampaignStatus.Completed, CampaignStatus.Cancelled };

    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PromotionService> _logger;

    public PromotionService(
        AppDbContext context,
        TimeProvider timeProvider,
        ILogger<PromotionService> logger)
    {
        _context = context;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<PagedResponse<PromotionResponse>> GetPromotionsAsync(
        bool canManagePromotions,
        PromotionQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var promotions = VisiblePromotions(canManagePromotions);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            promotions = promotions.Where(p => p.Name.ToLower().Contains(search));
        }

        if (query.Type is not null)
        {
            promotions = promotions.Where(p => p.Type == query.Type.Value);
        }

        if (query.IsActive is not null)
        {
            promotions = promotions.Where(p => p.IsActive == query.IsActive.Value);
        }

        if (query.CampaignId is not null)
        {
            promotions = promotions.Where(p => p.CampaignId == query.CampaignId.Value);
        }

        if (query.ActiveOn is not null)
        {
            var activeOn = MarketingDates.ToUtc(query.ActiveOn.Value);
            promotions = promotions.Where(
                p => p.StartDate <= activeOn && p.EndDate >= activeOn);
        }

        promotions = ApplySorting(promotions, query);

        var totalCount = await promotions.CountAsync(cancellationToken);

        var items = await promotions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResponse<PromotionResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PromotionResponse?> GetPromotionByIdAsync(
        bool canManagePromotions,
        Guid promotionId,
        CancellationToken cancellationToken = default)
    {
        return await VisiblePromotions(canManagePromotions)
            .Where(p => p.Id == promotionId)
            .Select(ToResponse)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PromotionResponse> CreatePromotionAsync(
        PromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await CreatePromotionsAsync(new[] { request }, cancellationToken);
        return created[0];
    }

    public async Task<List<PromotionResponse>> CreatePromotionsAsync(
        IReadOnlyList<PromotionRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return new List<PromotionResponse>();
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var promotions = new List<Promotion>();

            foreach (var request in requests)
            {
                var promotion = new Promotion();
                await ApplyRequestAsync(promotion, request, cancellationToken);
                _context.Promotions.Add(promotion);
                promotions.Add(promotion);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            foreach (var promotion in promotions)
            {
                _logger.LogInformation(
                    "Promotion {PromotionId} '{PromotionName}' created.",
                    promotion.Id,
                    promotion.Name);
            }

            var responses = new List<PromotionResponse>();
            foreach (var promotion in promotions)
            {
                responses.Add((await GetPromotionByIdAsync(true, promotion.Id, cancellationToken))!);
            }

            return responses;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PromotionResponse?> UpdatePromotionAsync(
        Guid promotionId,
        PromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var promotion = await _context.Promotions
                .Include(p => p.PromotionProducts)
                .Include(p => p.PromotionCategories)
                .FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken);

            if (promotion is null)
            {
                return null;
            }

            await ApplyRequestAsync(promotion, request, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Promotion {PromotionId} updated.",
                promotion.Id);

            return await GetPromotionByIdAsync(true, promotion.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PromotionTargetsResponse> GetTargetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var products = await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PromotionTargetOption
            {
                Id = p.Id,
                Name = p.Name,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new PromotionTargetOption
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);

        return new PromotionTargetsResponse
        {
            Products = products,
            Categories = categories
        };
    }

    public async Task<bool> DeletePromotionAsync(
        Guid promotionId,
        CancellationToken cancellationToken = default)
    {
        var promotion = await _context.Promotions
            .Include(p => p.Coupons)
            .FirstOrDefaultAsync(p => p.Id == promotionId, cancellationToken);

        if (promotion is null)
        {
            return false;
        }

        if (promotion.Coupons.Count > 0)
        {
            throw new InvalidOperationException(
                "Promotion has coupons and cannot be deleted. Deactivate it instead.");
        }

        _context.Promotions.Remove(promotion);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Promotion {PromotionId} deleted.", promotionId);

        return true;
    }

    private IQueryable<Promotion> VisiblePromotions(bool canManagePromotions)
    {
        var promotions = _context.Promotions.AsNoTracking();

        if (canManagePromotions)
        {
            return promotions;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return promotions.Where(p =>
            p.IsActive
            && p.StartDate <= now
            && p.EndDate >= now
            && (p.Campaign == null || p.Campaign.Status == CampaignStatus.Active));
    }

    private async Task ApplyRequestAsync(
        Promotion promotion,
        PromotionRequest request,
        CancellationToken cancellationToken)
    {
        var startDate = MarketingDates.ToUtc(request.StartDate!.Value);
        var endDate = MarketingDates.ToUtc(request.EndDate!.Value);

        if (request.CampaignId is not null)
        {
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == request.CampaignId.Value, cancellationToken);

            if (campaign is null)
            {
                throw new ArgumentException("Campaign not found.");
            }

            if (ClosedCampaignStatuses.Contains(campaign.Status))
            {
                throw new InvalidOperationException(
                    $"Promotions cannot be added to a {campaign.Status.ToString().ToLowerInvariant()} campaign.");
            }

            if (startDate < campaign.StartDate || endDate > campaign.EndDate)
            {
                throw new InvalidOperationException(
                    "Promotion dates must fall within the campaign's dates.");
            }
        }

        var productIds = request.ProductIds.Distinct().ToList();
        var categoryIds = request.CategoryIds.Distinct().ToList();

        var foundProducts = await _context.Products
            .CountAsync(p => productIds.Contains(p.Id), cancellationToken);

        if (foundProducts != productIds.Count)
        {
            throw new ArgumentException("One or more products were not found.");
        }

        var foundCategories = await _context.Categories
            .CountAsync(c => categoryIds.Contains(c.Id), cancellationToken);

        if (foundCategories != categoryIds.Count)
        {
            throw new ArgumentException("One or more categories were not found.");
        }

        promotion.Name = request.Name.Trim();
        promotion.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        promotion.Type = request.Type!.Value;
        promotion.DiscountValue = Math.Round(request.DiscountValue, 2);
        promotion.StartDate = startDate;
        promotion.EndDate = endDate;
        promotion.IsActive = request.IsActive;
        promotion.CampaignId = request.CampaignId;

        // Sync targets by difference so unchanged join rows are left untouched.
        foreach (var existing in promotion.PromotionProducts
                     .Where(pp => !productIds.Contains(pp.ProductId))
                     .ToList())
        {
            promotion.PromotionProducts.Remove(existing);
        }

        foreach (var productId in productIds
                     .Except(promotion.PromotionProducts.Select(pp => pp.ProductId))
                     .ToList())
        {
            promotion.PromotionProducts.Add(new PromotionProduct { ProductId = productId });
        }

        foreach (var existing in promotion.PromotionCategories
                     .Where(pc => !categoryIds.Contains(pc.CategoryId))
                     .ToList())
        {
            promotion.PromotionCategories.Remove(existing);
        }

        foreach (var categoryId in categoryIds
                     .Except(promotion.PromotionCategories.Select(pc => pc.CategoryId))
                     .ToList())
        {
            promotion.PromotionCategories.Add(new PromotionCategory { CategoryId = categoryId });
        }
    }

    private static IQueryable<Promotion> ApplySorting(
        IQueryable<Promotion> promotions,
        PromotionQuery query)
    {
        var descending = MarketingDates.IsDescending(query.SortDirection);

        return query.SortBy?.ToLowerInvariant() switch
        {
            "name" => descending
                ? promotions.OrderByDescending(p => p.Name)
                : promotions.OrderBy(p => p.Name),
            "enddate" => descending
                ? promotions.OrderByDescending(p => p.EndDate)
                : promotions.OrderBy(p => p.EndDate),
            // Cast keeps sorting provider-agnostic (SQLite cannot order by decimal).
            "discountvalue" => descending
                ? promotions.OrderByDescending(p => (double)p.DiscountValue)
                : promotions.OrderBy(p => (double)p.DiscountValue),
            "createdat" => descending
                ? promotions.OrderByDescending(p => p.CreatedAt)
                : promotions.OrderBy(p => p.CreatedAt),
            _ => descending
                ? promotions.OrderByDescending(p => p.StartDate)
                : promotions.OrderBy(p => p.StartDate)
        };
    }

    private static readonly System.Linq.Expressions.Expression<Func<Promotion, PromotionResponse>>
        ToResponse = p => new PromotionResponse
        {
            Id = p.Id,
            CampaignId = p.CampaignId,
            CampaignName = p.Campaign == null ? null : p.Campaign.Name,
            Name = p.Name,
            Description = p.Description,
            Type = p.Type,
            DiscountValue = p.DiscountValue,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            IsActive = p.IsActive,
            ProductIds = p.PromotionProducts.Select(pp => pp.ProductId).ToList(),
            CategoryIds = p.PromotionCategories.Select(pc => pc.CategoryId).ToList(),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
}
