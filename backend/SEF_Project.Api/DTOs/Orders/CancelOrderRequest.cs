using System.ComponentModel.DataAnnotations;

namespace SEF_Project.Api.DTOs.Orders;

public class CancelOrderRequest
{
    [StringLength(1000)]
    public string? Reason { get; set; }
}
