using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.Orders;

public class ProductReturn : GuidEntity
{
    public string ReturnNumber { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;

    public ReturnReason Reason { get; set; }

    public string? CustomerNote { get; set; }

    public string? StaffNote { get; set; }

    public decimal RefundAmount { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public DateTime? RefundedAt { get; set; }

    public int? ReviewedByUserId { get; set; }

    public User? ReviewedByUser { get; set; }

    public ICollection<ReturnItem> Items { get; set; } =
        new List<ReturnItem>();
}
