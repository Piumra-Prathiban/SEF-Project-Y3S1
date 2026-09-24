using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;

namespace SEF_Project.Api.Services.Marketing;

public interface ICampaignService
{
    Task<PagedResponse<CampaignResponse>> GetCampaignsAsync(
        CampaignQuery query,
        CancellationToken cancellationToken = default);

    Task<CampaignResponse?> GetCampaignByIdAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CampaignResponse> CreateCampaignAsync(
        CampaignRequest request,
        CancellationToken cancellationToken = default);

    Task<CampaignResponse?> UpdateCampaignAsync(
        Guid campaignId,
        CampaignRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns false when the campaign does not exist.</summary>
    Task<bool> DeleteCampaignAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);
}
