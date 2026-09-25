using SEF_Project.Api.DTOs.Common;

namespace SEF_Project.Api.DTOs.Shopping;

public class AddWishlistItemRequest
{
    [NotEmptyGuid]
    public Guid ProductId { get; set; }
}

public class WishlistResponse
{
    public Guid? Id { get; set; }

    public IReadOnlyList<WishlistItemResponse> Items { get; set; } =
        Array.Empty<WishlistItemResponse>();

    public int Count => Items.Count;
}

public class WishlistItemResponse
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal? MinimumPrice { get; set; }

    public bool IsProductActive { get; set; }

    public bool IsAvailable { get; set; }

    public DateTime AddedAt { get; set; }
}

public class WishlistCountResponse
{
    public int Count { get; set; }
}
