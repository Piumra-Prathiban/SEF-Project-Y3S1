using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.DTOs.Orders;

public class CreateShipmentRequest
{
    [StringLength(100)]
    public string? Carrier { get; set; }

    [StringLength(100)]
    public string? TrackingNumber { get; set; }
}

public class UpdateShipmentRequest
{
    [Required]
    [EnumDataType(typeof(ShipmentStatus))]
    public ShipmentStatus Status { get; set; }

    [StringLength(100)]
    public string? Carrier { get; set; }

    [StringLength(100)]
    public string? TrackingNumber { get; set; }
}
