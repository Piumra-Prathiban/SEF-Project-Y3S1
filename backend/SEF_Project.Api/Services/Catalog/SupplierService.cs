using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models.Catalog;

namespace SEF_Project.Api.Services.Catalog;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;

    public SupplierService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SupplierResponseDto>> GetSuppliersAsync(
        SupplierQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var suppliersQuery = _context.Suppliers
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            suppliersQuery = suppliersQuery.Where(
                s => s.Name.ToLower().Contains(search)
                     || (s.ContactName != null && s.ContactName.ToLower().Contains(search))
                     || (s.Email != null && s.Email.ToLower().Contains(search)));
        }

        if (query.IsActive is not null)
        {
            suppliersQuery = suppliersQuery.Where(
                s => s.IsActive == query.IsActive.Value);
        }

        var suppliers = await suppliersQuery
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return suppliers.Select(MapSupplier).ToList();
    }

    public async Task<SupplierResponseDto?> GetSupplierByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return supplier is null ? null : MapSupplier(supplier);
    }

    public async Task<SupplierResponseDto> CreateSupplierAsync(
        SupplierCreateDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureUniqueSupplierNameAsync(
            request.Name,
            excludingId: null,
            cancellationToken);

        var supplier = new Supplier
        {
            Name = request.Name.Trim(),
            ContactName = NullIfWhitespace(request.ContactName),
            Email = NullIfWhitespace(request.Email),
            Phone = NullIfWhitespace(request.Phone),
            IsActive = request.IsActive
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return MapSupplier(supplier);
    }

    public async Task<SupplierResponseDto?> UpdateSupplierAsync(
        Guid id,
        SupplierUpdateDto request,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            return null;
        }

        await EnsureUniqueSupplierNameAsync(
            request.Name,
            excludingId: id,
            cancellationToken);

        supplier.Name = request.Name.Trim();
        supplier.ContactName = NullIfWhitespace(request.ContactName);
        supplier.Email = NullIfWhitespace(request.Email);
        supplier.Phone = NullIfWhitespace(request.Phone);
        supplier.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return MapSupplier(supplier);
    }

    public async Task<bool> DeleteSupplierAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            return false;
        }

        supplier.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureUniqueSupplierNameAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();

        var exists = await _context.Suppliers.AnyAsync(
            s => s.Name.ToLower() == normalized
                 && (excludingId == null || s.Id != excludingId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A supplier with this name already exists.");
        }
    }

    private static SupplierResponseDto MapSupplier(Supplier supplier) =>
        new()
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactName = supplier.ContactName,
            Email = supplier.Email,
            Phone = supplier.Phone,
            IsActive = supplier.IsActive,
            CreatedAt = supplier.CreatedAt,
            UpdatedAt = supplier.UpdatedAt
        };

    private static string? NullIfWhitespace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
