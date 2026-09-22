using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class CreateOrderRequest
{
    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; set; } = new();

    [Required]
    public CreateOrderAddressRequest DeliveryAddress { get; set; } = new();

    [Required]
    [EnumDataType(typeof(PaymentMethod))]
    public PaymentMethod PaymentMethod { get; set; }

    [StringLength(50)]
    public string? CouponCode { get; set; }
}

public class CreateOrderItemRequest
{
    [NotEmptyGuid]
    public Guid ProductVariantId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class CreateOrderAddressRequest
{
    [Required]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Line1 { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Line2 { get; set; }

    [Required]
    [StringLength(100)]
    public string City { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Province { get; set; }

    [Required]
    [StringLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Country { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }
}
