using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Tests;

public class SupplierServiceTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Context)>
        CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    [Fact]
    public async Task GetSuppliersAsync_ShouldReturnSeededSuppliersOrderedByName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var suppliers = await service.GetSuppliersAsync(new SupplierQueryDto());

        Assert.Equal(
            new[] { "Atlas Textiles", "Nordic Footwear" },
            suppliers.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task GetSuppliersAsync_ShouldSearchByNameOrContact()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var byName = await service.GetSuppliersAsync(
            new SupplierQueryDto { Search = "nordic" });
        var byContact = await service.GetSuppliersAsync(
            new SupplierQueryDto { Search = "Nimal" });

        Assert.Equal("Nordic Footwear", Assert.Single(byName).Name);
        Assert.Equal("Atlas Textiles", Assert.Single(byContact).Name);
    }

    [Fact]
    public async Task GetSuppliersAsync_ShouldFilterByIsActive()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        await service.CreateSupplierAsync(new SupplierCreateDto
        {
            Name = "Dormant Threads",
            IsActive = false
        });

        var active = await service.GetSuppliersAsync(
            new SupplierQueryDto { IsActive = true });
        var inactive = await service.GetSuppliersAsync(
            new SupplierQueryDto { IsActive = false });

        Assert.Equal(
            new[] { "Atlas Textiles", "Nordic Footwear" },
            active.Select(s => s.Name).ToArray());
        Assert.Equal("Dormant Threads", Assert.Single(inactive).Name);
    }

    [Fact]
    public async Task GetSupplierByIdAsync_ShouldReturnSupplierAndNullWhenMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var found = await service.GetSupplierByIdAsync(SeedData.SupplierAtlasTextiles);
        var missing = await service.GetSupplierByIdAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal("Atlas Textiles", found!.Name);
        Assert.Equal("Nimal Perera", found.ContactName);
        Assert.Equal("orders@atlastextiles.lk", found.Email);
        Assert.Null(missing);
    }

    [Fact]
    public async Task SupplierCrud_ShouldCreateReadUpdateAndSoftDelete()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var created = await service.CreateSupplierAsync(new SupplierCreateDto
        {
            Name = "  Loom & Thread  ",
            ContactName = "  Ayesha Fernando  ",
            Email = "  hello@loomandthread.lk  ",
            Phone = "  +94 77 123 4567  "
        });

        var read = await service.GetSupplierByIdAsync(created.Id);
        var updated = await service.UpdateSupplierAsync(created.Id, new SupplierUpdateDto
        {
            Name = "Loom and Thread Studio",
            ContactName = "Ayesha Fernando",
            Email = "studio@loomandthread.lk",
            Phone = "+94 77 765 4321",
            IsActive = true
        });
        var deleted = await service.DeleteSupplierAsync(created.Id);
        var stored = await context.Suppliers.SingleAsync(s => s.Id == created.Id);

        Assert.Equal("Loom & Thread", created.Name);
        Assert.Equal("Ayesha Fernando", created.ContactName);
        Assert.Equal("hello@loomandthread.lk", created.Email);
        Assert.Equal("+94 77 123 4567", created.Phone);
        Assert.NotNull(read);
        Assert.Equal("Loom and Thread Studio", updated?.Name);
        Assert.Equal("+94 77 765 4321", updated?.Phone);
        Assert.True(deleted);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldBlankOptionalFieldsWhenWhitespace()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var created = await service.CreateSupplierAsync(new SupplierCreateDto
        {
            Name = "Minimal Atelier",
            ContactName = "   ",
            Email = "",
            Phone = null
        });

        Assert.Null(created.ContactName);
        Assert.Null(created.Email);
        Assert.Null(created.Phone);
    }

    [Fact]
    public async Task CreateSupplierAsync_ShouldRejectDuplicateName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSupplierAsync(new SupplierCreateDto
            {
                Name = "atlas textiles"
            }));

        Assert.Equal("A supplier with this name already exists.", exception.Message);
    }

    [Fact]
    public async Task UpdateSupplierAsync_ShouldRejectDuplicateName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateSupplierAsync(
                SeedData.SupplierNordicFootwear,
                new SupplierUpdateDto { Name = "Atlas Textiles", IsActive = true }));

        Assert.Equal("A supplier with this name already exists.", exception.Message);
    }

    [Fact]
    public async Task UpdateSupplierAsync_ShouldReturnNullWhenMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var updated = await service.UpdateSupplierAsync(
            Guid.NewGuid(),
            new SupplierUpdateDto { Name = "Ghost Supplier", IsActive = true });

        Assert.Null(updated);
    }

    [Fact]
    public async Task DeleteSupplierAsync_ShouldReturnFalseWhenMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var deleted = await service.DeleteSupplierAsync(Guid.NewGuid());

        Assert.False(deleted);
    }

    [Fact]
    public async Task SoftDeletedSupplier_ShouldRemainReadableButBeFilteredOut()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new SupplierService(context);

        var deleted = await service.DeleteSupplierAsync(SeedData.SupplierAtlasTextiles);
        var stillReadable = await service.GetSupplierByIdAsync(
            SeedData.SupplierAtlasTextiles);
        var active = await service.GetSuppliersAsync(
            new SupplierQueryDto { IsActive = true });

        Assert.True(deleted);
        Assert.NotNull(stillReadable);
        Assert.False(stillReadable!.IsActive);
        Assert.Equal("Nordic Footwear", Assert.Single(active).Name);
    }

    [Fact]
    public async Task ProductReferencingSupplier_ShouldStillResolveSupplierName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var supplierService = new SupplierService(context);
        var catalogService = new CatalogService(context);

        var supplier = await supplierService.CreateSupplierAsync(new SupplierCreateDto
        {
            Name = "Loom & Thread"
        });
        var categoryId = await context.Categories.Select(c => c.Id).FirstAsync();
        var collectionId = await context.Collections.Select(c => c.Id).FirstAsync();

        var product = await catalogService.CreateProductAsync(new ProductCreateDto
        {
            Name = "Merino Wool Scarf",
            CategoryId = categoryId,
            CollectionId = collectionId,
            SupplierId = supplier.Id
        });

        await supplierService.DeleteSupplierAsync(supplier.Id);

        var reloaded = await catalogService.GetProductByIdAsync(product.Id);

        Assert.Equal(supplier.Id, reloaded?.SupplierId);
        Assert.Equal("Loom & Thread", reloaded?.SupplierName);
    }
}
