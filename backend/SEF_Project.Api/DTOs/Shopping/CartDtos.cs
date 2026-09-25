using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Common;

namespace SEF_Project.Api.DTOs.Shopping;

public class AddCartItemRequest
{
    [NotEmptyGuid]
    public Guid ProductVariantId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class UpdateCartItemRequest
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class CartResponse
{
    public Guid? Id { get; set; }

    public string Currency { get; set; } = "LKR";

    public IReadOnlyList<CartItemResponse> Items { get; set; } =
        Array.Empty<CartItemResponse>();

    public int TotalQuantity { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }
}

public class CartItemResponse
{
    public Guid Id { get; set; }

    public Guid ProductVariantId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string VariantName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal { get; set; }

    public int AvailableQuantity { get; set; }

    public bool IsAvailable { get; set; }

    public bool HasSufficientStock { get; set; }
}
