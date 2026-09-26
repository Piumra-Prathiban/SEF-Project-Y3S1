using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Marketing;

public class CampaignQuery
{
    [Range(1, PagingLimits.MaxPage)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>name, startDate, endDate or createdAt (default startDate).</summary>
    [StringLength(50)]
    public string? SortBy { get; set; }

    /// <summary>asc or desc (default desc).</summary>
    [StringLength(4)]
    public string? SortDirection { get; set; }

    [StringLength(200)]
    public string? Search { get; set; }

    [EnumDataType(typeof(CampaignStatus))]
    public CampaignStatus? Status { get; set; }

    /// <summary>Only campaigns whose date window contains this instant.</summary>
    public DateTime? ActiveOn { get; set; }
}
