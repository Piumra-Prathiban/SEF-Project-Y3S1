using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class UpdateOrderStatusRequest
{
    [Required]
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus Status { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}
