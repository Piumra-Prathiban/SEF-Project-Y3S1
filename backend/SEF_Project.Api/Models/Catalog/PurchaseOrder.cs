using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.Catalog;

public class PurchaseOrder : GuidEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Supplier Supplier { get; set; } = null!;

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateTime? ExpectedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public string? Notes { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } =
        new List<PurchaseOrderItem>();
}
