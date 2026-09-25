using SEF_Project.Api.DTOs.Storefront;

namespace SEF_Project.Api.Services.Storefront;

public interface IStorefrontService
{
    Task<List<StorefrontProductResponseDto>> GetProductsAsync(
        StorefrontProductQueryDto query,
        CancellationToken cancellationToken = default);

    Task<StorefrontProductResponseDto?> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
