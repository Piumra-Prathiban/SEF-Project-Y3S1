using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class CreatePaymentRequest
{
    [Required]
    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod Method { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}

public class UpdatePaymentStatusRequest
{
    [Required]
    [EnumDataType(typeof(PaymentStatus))]
    public PaymentStatus Status { get; set; }

    [StringLength(200)]
    public string? TransactionReference { get; set; }
}
