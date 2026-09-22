using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Tests;

public class ProductServiceTests
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
    public async Task CreateProduct_WithVariantsAndImages_ShouldSucceedAndSeedStock()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var category = await service.CreateCategoryAsync(new CreateCategoryRequest { Name = "Pizza Specials" });

        var request = new CreateProductRequest
        {
            Name = "Supreme Pizza",
            Description = "Loaded with fresh toppings",
            CategoryIds = new List<Guid> { category.Id },
            InitialVariants = new List<CreateProductVariantRequest>
            {
                new()
                {
                    Sku = "PIZ-SUP-REG",
                    Name = "Regular",
                    Size = "Medium",
                    Colour = "Golden Crust",
                    Price = 1850m,
                    InitialStock = 25,
                    ReorderLevel = 5
                },
                new()
                {
                    Sku = "PIZ-SUP-LRG",
                    Name = "Large",
                    Size = "Large",
                    Colour = "Golden Crust",
                    Price = 2850m,
                    InitialStock = 15,
                    ReorderLevel = 5
                }
            },
            InitialImages = new List<CreateProductImageRequest>
            {
                new()
                {
                    ImageUrl = "https://example.com/supreme.jpg",
                    AltText = "Supreme pizza hero",
                    IsPrimary = true
                }
            }
        };

        var response = await service.CreateProductAsync(request);

        Assert.NotNull(response);
        Assert.Equal("Supreme Pizza", response.Name);
        Assert.Equal(2, response.Variants.Count);
        Assert.Single(response.Images);
        Assert.True(response.Images.First().IsPrimary);
        Assert.Single(response.Categories);

        var firstVariant = response.Variants.First(v => v.Sku == "PIZ-SUP-REG");
        Assert.Equal("Medium", firstVariant.Size);
        Assert.Equal("Golden Crust", firstVariant.Colour);
        Assert.Equal(25, firstVariant.QuantityOnHand);

        // Verify InventoryTransaction was created for initial stock
        var tx = await context.InventoryTransactions
            .FirstOrDefaultAsync(t => t.ProductVariantId == firstVariant.Id);
        Assert.NotNull(tx);
        Assert.Equal(InventoryTransactionType.Receipt, tx.Type);
        Assert.Equal(25, tx.QuantityChange);
    }

    [Fact]
    public async Task CreateVariant_ShouldAllocateInventoryAndThrow_OnDuplicateSku()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var product = await service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Tacos"
        });

        var variant = await service.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "TAC-BEEF-3",
            Name = "3 Beef Tacos",
            Size = "Regular",
            Colour = "Crispy",
            Price = 950m,
            InitialStock = 40,
            ReorderLevel = 10
        });

        Assert.Equal("TAC-BEEF-3", variant.Sku);
        Assert.Equal(40, variant.QuantityOnHand);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateVariantAsync(product.Id, new CreateProductVariantRequest
            {
                Sku = "tac-beef-3",
                Name = "Duplicate Beef Tacos",
                Price = 950m
            }));
    }

    [Fact]
    public async Task UpdateVariant_ShouldUpdateSizeColourPrice()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var product = await service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Wings"
        });

        var variant = await service.CreateVariantAsync(product.Id, new CreateProductVariantRequest
        {
            Sku = "WNG-6",
            Name = "6 pcs Wings",
            Price = 600m
        });

        var updated = await service.UpdateVariantAsync(product.Id, variant.Id, new UpdateProductVariantRequest
        {
            Sku = "WNG-6",
            Name = "6 pcs Spicy Wings",
            Size = "6 pcs",
            Colour = "Hot Red",
            Price = 750m,
            ReorderLevel = 15,
            IsActive = true
        });

        Assert.NotNull(updated);
        Assert.Equal("6 pcs Spicy Wings", updated.Name);
        Assert.Equal("6 pcs", updated.Size);
        Assert.Equal("Hot Red", updated.Colour);
        Assert.Equal(750m, updated.Price);
        Assert.Equal(15, updated.ReorderLevel);
    }

    [Fact]
    public async Task ImageManagement_ShouldHandlePrimaryAndOrdering()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var product = await service.CreateProductAsync(new CreateProductRequest { Name = "Burger" });

        var img1 = await service.AddImageAsync(product.Id, new CreateProductImageRequest
        {
            ImageUrl = "https://example.com/img1.jpg",
            IsPrimary = false // First image automatically becomes primary
        });

        Assert.True(img1.IsPrimary);

        var img2 = await service.AddImageAsync(product.Id, new CreateProductImageRequest
        {
            ImageUrl = "https://example.com/img2.jpg",
            IsPrimary = false
        });

        Assert.False(img2.IsPrimary);

        // Switch primary
        var setPrimary = await service.SetPrimaryImageAsync(product.Id, img2.Id);
        Assert.True(setPrimary);

        var productDetails = await service.GetProductByIdAsync(product.Id);
        Assert.NotNull(productDetails);
        var refreshedImg2 = productDetails.Images.First(i => i.Id == img2.Id);
        var refreshedImg1 = productDetails.Images.First(i => i.Id == img1.Id);
        Assert.True(refreshedImg2.IsPrimary);
        Assert.False(refreshedImg1.IsPrimary);

        // Delete primary image should promote remaining image
        await service.DeleteImageAsync(product.Id, img2.Id);
        var afterDelete = await service.GetProductByIdAsync(product.Id);
        Assert.NotNull(afterDelete);
        Assert.Single(afterDelete.Images);
        Assert.True(afterDelete.Images.First().IsPrimary);
    }

    [Fact]
    public async Task GetProducts_WithSearchAndCategoryFilters_ShouldWork()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var catA = await service.CreateCategoryAsync(new CreateCategoryRequest { Name = "Main Dishes" });
        var catB = await service.CreateCategoryAsync(new CreateCategoryRequest { Name = "Sides" });

        await service.CreateProductAsync(new CreateProductRequest
        {
            Name = "Cheese Burger",
            CategoryIds = new List<Guid> { catA.Id }
        });

        await service.CreateProductAsync(new CreateProductRequest
        {
            Name = "French Fries",
            CategoryIds = new List<Guid> { catB.Id }
        });

        var burgerResults = await service.GetProductsAsync(new ProductQuery { Search = "burger" });
        Assert.Single(burgerResults.Items);
        Assert.Equal("Cheese Burger", burgerResults.Items.First().Name);

        var sidesResults = await service.GetProductsAsync(new ProductQuery { CategoryId = catB.Id });
        Assert.Single(sidesResults.Items);
        Assert.Equal("French Fries", sidesResults.Items.First().Name);
    }
}
