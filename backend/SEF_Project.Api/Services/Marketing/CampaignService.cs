using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;

namespace SEF_Project.Api.Services.Marketing;

public class CampaignService : ICampaignService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(
        AppDbContext context,
        ILogger<CampaignService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResponse<CampaignResponse>> GetCampaignsAsync(
        CampaignQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var campaigns = _context.Campaigns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            campaigns = campaigns.Where(c => c.Name.ToLower().Contains(search));
        }

        if (query.Status is not null)
        {
            campaigns = campaigns.Where(c => c.Status == query.Status.Value);
        }

        if (query.ActiveOn is not null)
        {
            var activeOn = MarketingDates.ToUtc(query.ActiveOn.Value);
            campaigns = campaigns.Where(
                c => c.StartDate <= activeOn && c.EndDate >= activeOn);
        }

        campaigns = ApplySorting(campaigns, query);

        var totalCount = await campaigns.CountAsync(cancellationToken);

        var items = await campaigns
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResponse<CampaignResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CampaignResponse?> GetCampaignByIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Campaigns
            .AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(ToResponse)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CampaignResponse> CreateCampaignAsync(
        CampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        var campaign = new Campaign();
        ApplyRequest(campaign, request);

        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Campaign {CampaignId} '{CampaignName}' created.",
            campaign.Id,
            campaign.Name);

        return (await GetCampaignByIdAsync(campaign.Id, cancellationToken))!;
    }

    public async Task<CampaignResponse?> UpdateCampaignAsync(
        Guid campaignId,
        CampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Promotions)
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);

        if (campaign is null)
        {
            return null;
        }

        if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Cancelled
            && request.Status != campaign.Status)
        {
            throw new InvalidOperationException(
                $"A {campaign.Status.ToString().ToLowerInvariant()} campaign cannot change status.");
        }

        var startDate = MarketingDates.ToUtc(request.StartDate!.Value);
        var endDate = MarketingDates.ToUtc(request.EndDate!.Value);

        if (campaign.Promotions.Any(p => p.StartDate < startDate || p.EndDate > endDate))
        {
            throw new InvalidOperationException(
                "Campaign dates must still cover all of its promotions.");
        }

        ApplyRequest(campaign, request);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Campaign {CampaignId} updated (status {Status}).",
            campaign.Id,
            campaign.Status);

        return await GetCampaignByIdAsync(campaign.Id, cancellationToken);
    }

    public async Task<bool> DeleteCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        var campaign = await _context.Campaigns
            .Include(c => c.Promotions)
            .FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);

        if (campaign is null)
        {
            return false;
        }

        if (campaign.Promotions.Count > 0)
        {
            throw new InvalidOperationException(
                "Campaign has promotions and cannot be deleted. Cancel it instead.");
        }

        _context.Campaigns.Remove(campaign);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Campaign {CampaignId} deleted.", campaignId);

        return true;
    }

    private static void ApplyRequest(Campaign campaign, CampaignRequest request)
    {
        campaign.Name = request.Name.Trim();
        campaign.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        campaign.StartDate = MarketingDates.ToUtc(request.StartDate!.Value);
        campaign.EndDate = MarketingDates.ToUtc(request.EndDate!.Value);
        campaign.Status = request.Status;
    }

    private static IQueryable<Campaign> ApplySorting(
        IQueryable<Campaign> campaigns,
        CampaignQuery query)
    {
        var descending = MarketingDates.IsDescending(query.SortDirection);

        return query.SortBy?.ToLowerInvariant() switch
        {
            "name" => descending
                ? campaigns.OrderByDescending(c => c.Name)
                : campaigns.OrderBy(c => c.Name),
            "enddate" => descending
                ? campaigns.OrderByDescending(c => c.EndDate)
                : campaigns.OrderBy(c => c.EndDate),
            "createdat" => descending
                ? campaigns.OrderByDescending(c => c.CreatedAt)
                : campaigns.OrderBy(c => c.CreatedAt),
            _ => descending
                ? campaigns.OrderByDescending(c => c.StartDate)
                : campaigns.OrderBy(c => c.StartDate)
        };
    }

    private static readonly System.Linq.Expressions.Expression<Func<Campaign, CampaignResponse>>
        ToResponse = c => new CampaignResponse
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Status = c.Status,
            PromotionCount = c.Promotions.Count,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
}
