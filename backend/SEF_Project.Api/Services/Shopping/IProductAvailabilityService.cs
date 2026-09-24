namespace SEF_Project.Api.Services.Shopping;

public sealed record AvailableProductVariant(
    Guid ProductId,
    Guid VariantId,
    string ProductName,
    string VariantName,
    string Sku,
    decimal Price,
    int AvailableQuantity);

public interface IProductAvailabilityService
{
    Task<IReadOnlyList<AvailableProductVariant>> GetAvailableVariantsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken = default);
}
