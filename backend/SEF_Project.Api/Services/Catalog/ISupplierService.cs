using SEF_Project.Api.DTOs.Catalog;

namespace SEF_Project.Api.Services.Catalog;

/// <summary>
/// Supplier master-data management for the catalog. Suppliers can be
/// soft-deleted by deactivating them so retained products keep their link.
/// </summary>
public interface ISupplierService
{
    /// <summary>Lists suppliers, optionally searched and filtered by status.</summary>
    Task<List<SupplierResponseDto>> GetSuppliersAsync(
        SupplierQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single supplier or <c>null</c> when it does not exist.</summary>
    Task<SupplierResponseDto?> GetSupplierByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a supplier. Throws <see cref="InvalidOperationException"/> when a
    /// supplier with the same name already exists.
    /// </summary>
    Task<SupplierResponseDto> CreateSupplierAsync(
        SupplierCreateDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a supplier or returns <c>null</c> when it does not exist. Throws
    /// <see cref="InvalidOperationException"/> on a duplicate name.
    /// </summary>
    Task<SupplierResponseDto?> UpdateSupplierAsync(
        Guid id,
        SupplierUpdateDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a supplier by deactivating it.</summary>
    Task<bool> DeleteSupplierAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
