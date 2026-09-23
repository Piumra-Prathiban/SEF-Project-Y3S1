using SEF_Project.Api.DTOs.Shopping;

namespace SEF_Project.Api.Services.Shopping;

public enum AddWishlistItemStatus
{
    Added,
    ProductNotFound,
    Duplicate
}

public record AddWishlistItemResult(
    AddWishlistItemStatus Status,
    WishlistItemResponse? Item = null);

public interface IWishlistService
{
    Task<WishlistResponse> GetWishlistAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<AddWishlistItemResult> AddItemAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveItemAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
