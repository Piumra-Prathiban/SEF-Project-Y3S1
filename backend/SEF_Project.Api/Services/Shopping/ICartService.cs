using SEF_Project.Api.DTOs.Shopping;

namespace SEF_Project.Api.Services.Shopping;

public enum CartMutationStatus
{
    Added,
    Updated,
    ItemNotFound,
    VariantNotFound,
    VariantUnavailable,
    InsufficientStock
}

public record CartMutationResult(
    CartMutationStatus Status,
    CartResponse? Cart = null);

public interface ICartService
{
    Task<CartResponse> GetCartAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<CartMutationResult> AddItemAsync(
        int userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken = default);

    Task<CartMutationResult> UpdateItemAsync(
        int userId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveItemAsync(
        int userId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task ClearCartAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
