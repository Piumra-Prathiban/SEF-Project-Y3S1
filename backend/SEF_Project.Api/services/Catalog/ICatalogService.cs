using SEF_Project.Api.DTOs.Catalog;

namespace SEF_Project.Api.Services.Catalog;

public interface ICatalogService
{
    Task<List<CategoryResponseDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<CategoryResponseDto?> GetCategoryByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CategoryResponseDto> CreateCategoryAsync(
        CategoryCreateDto request,
        CancellationToken cancellationToken = default);

    Task<CategoryResponseDto?> UpdateCategoryAsync(
        Guid id,
        CategoryUpdateDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<CollectionResponseDto>> GetCollectionsAsync(
        CancellationToken cancellationToken = default);

    Task<CollectionResponseDto?> GetCollectionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CollectionResponseDto> CreateCollectionAsync(
        CollectionCreateDto request,
        CancellationToken cancellationToken = default);

    Task<CollectionResponseDto?> UpdateCollectionAsync(
        Guid id,
        CollectionUpdateDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteCollectionAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<SizeResponseDto>> GetSizesAsync(
        CancellationToken cancellationToken = default);

    Task<SizeResponseDto?> GetSizeByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<SizeResponseDto> CreateSizeAsync(
        SizeCreateDto request,
        CancellationToken cancellationToken = default);

    Task<SizeResponseDto?> UpdateSizeAsync(
        Guid id,
        SizeUpdateDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSizeAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<ColourResponseDto>> GetColoursAsync(
        CancellationToken cancellationToken = default);

    Task<ColourResponseDto?> GetColourByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ColourResponseDto> CreateColourAsync(
        ColourCreateDto request,
        CancellationToken cancellationToken = default);

    Task<ColourResponseDto?> UpdateColourAsync(
        Guid id,
        ColourUpdateDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteColourAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<ProductResponseDto>> GetProductsAsync(
        CancellationToken cancellationToken = default);

    Task<ProductResponseDto?> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ProductResponseDto> CreateProductAsync(
        ProductCreateDto request,
        CancellationToken cancellationToken = default);

    Task<ProductResponseDto?> UpdateProductAsync(
        Guid id,
        ProductUpdateDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<ProductVariantResponseDto>> GetVariantsAsync(
        Guid? productId = null,
        CancellationToken cancellationToken = default);

    Task<ProductVariantResponseDto?> GetVariantByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ProductVariantResponseDto> CreateVariantAsync(
        ProductVariantCreateDto request,
        CancellationToken cancellationToken = default);

    Task<ProductVariantResponseDto?> UpdateVariantAsync(
        Guid id,
        ProductVariantUpdateDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteVariantAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
