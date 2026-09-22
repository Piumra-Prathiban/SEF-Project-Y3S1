using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Models.Orders;

public class Payment : GuidEntity
{
    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; }

    public string? TransactionReference { get; set; }

    public DateTime? PaidAt { get; set; }
}
