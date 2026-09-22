using SEF_Project.Api.DTOs.Catalog;

namespace SEF_Project.Api.Services.Catalog;

public interface ICatalogService
{
    // Categories
    Task<List<CategoryResponse>> GetCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CategoryResponse?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryResponse?> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    // Products
    Task<ProductListResponse> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductDetailResponse?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDetailResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailResponse?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);

    // Variants
    Task<ProductVariantResponse> CreateVariantAsync(Guid productId, CreateProductVariantRequest request, CancellationToken cancellationToken = default);
    Task<ProductVariantResponse?> UpdateVariantAsync(Guid productId, Guid variantId, UpdateProductVariantRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteVariantAsync(Guid productId, Guid variantId, CancellationToken cancellationToken = default);

    // Images
    Task<ProductImageResponse> AddImageAsync(Guid productId, CreateProductImageRequest request, CancellationToken cancellationToken = default);
    Task<ProductImageResponse?> UpdateImageAsync(Guid productId, Guid imageId, UpdateProductImageRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default);
    Task<bool> SetPrimaryImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default);
}
