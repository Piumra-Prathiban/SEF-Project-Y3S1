using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Tests;

public class CatalogControllerTests
{
    [Theory]
    [InlineData(typeof(ProductsController))]
    [InlineData(typeof(CategoriesController))]
    [InlineData(typeof(CollectionsController))]
    [InlineData(typeof(SizesController))]
    [InlineData(typeof(ColoursController))]
    [InlineData(typeof(VariantsController))]
    public void CatalogControllers_ShouldRequireAuthentication(Type controllerType)
    {
        var attribute = controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
    }

    [Theory]
    [InlineData(typeof(ProductsController), nameof(ProductsController.CreateProduct))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.UpdateProduct))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.DeleteProduct))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.CreateCategory))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.UpdateCategory))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.DeleteCategory))]
    [InlineData(typeof(CollectionsController), nameof(CollectionsController.CreateCollection))]
    [InlineData(typeof(CollectionsController), nameof(CollectionsController.UpdateCollection))]
    [InlineData(typeof(CollectionsController), nameof(CollectionsController.DeleteCollection))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.CreateProductVariant))]
    [InlineData(typeof(SizesController), nameof(SizesController.CreateSize))]
    [InlineData(typeof(SizesController), nameof(SizesController.UpdateSize))]
    [InlineData(typeof(SizesController), nameof(SizesController.DeleteSize))]
    [InlineData(typeof(ColoursController), nameof(ColoursController.CreateColour))]
    [InlineData(typeof(ColoursController), nameof(ColoursController.UpdateColour))]
    [InlineData(typeof(ColoursController), nameof(ColoursController.DeleteColour))]
    [InlineData(typeof(VariantsController), nameof(VariantsController.UpdateVariant))]
    [InlineData(typeof(VariantsController), nameof(VariantsController.DeleteVariant))]
    public void WriteActions_ShouldRequireStaffOrAdministrator(
        Type controllerType,
        string methodName)
    {
        var method = controllerType.GetMethod(methodName);
        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Staff,Administrator", attribute!.Roles);
    }

    [Theory]
    [InlineData(typeof(ProductsController), nameof(ProductsController.GetProducts))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.GetProductById))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.GetProductVariants))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.GetCategories))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.GetCategoryById))]
    [InlineData(typeof(CollectionsController), nameof(CollectionsController.GetCollections))]
    [InlineData(typeof(CollectionsController), nameof(CollectionsController.GetCollectionById))]
    [InlineData(typeof(SizesController), nameof(SizesController.GetSizes))]
    [InlineData(typeof(SizesController), nameof(SizesController.GetSizeById))]
    [InlineData(typeof(ColoursController), nameof(ColoursController.GetColours))]
    [InlineData(typeof(ColoursController), nameof(ColoursController.GetColourById))]
    [InlineData(typeof(VariantsController), nameof(VariantsController.GetVariantById))]
    public void ReadActions_ShouldRequireAuthenticatedUserWithoutSpecificRole(
        Type controllerType,
        string methodName)
    {
        var method = controllerType.GetMethod(methodName);
        Assert.NotNull(method);

        var allowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>();
        var methodAuthorize = method.GetCustomAttribute<AuthorizeAttribute>();
        var controllerAuthorize =
            controllerType.GetCustomAttribute<AuthorizeAttribute>();

        Assert.Null(allowAnonymous);
        Assert.True(methodAuthorize is not null || controllerAuthorize is not null);
        Assert.True(methodAuthorize is null || methodAuthorize.Roles is null);
    }

    [Theory]
    [InlineData(typeof(ProductsController), nameof(ProductsController.CreateProduct))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.UpdateProduct))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.DeleteProduct))]
    [InlineData(typeof(ProductsController), nameof(ProductsController.CreateProductVariant))]
    [InlineData(typeof(CategoriesController), nameof(CategoriesController.CreateCategory))]
    [InlineData(typeof(CollectionsController), nameof(CollectionsController.CreateCollection))]
    [InlineData(typeof(SizesController), nameof(SizesController.CreateSize))]
    [InlineData(typeof(ColoursController), nameof(ColoursController.CreateColour))]
    [InlineData(typeof(VariantsController), nameof(VariantsController.UpdateVariant))]
    public void WriteActions_ShouldNotAllowCustomerRole(
        Type controllerType,
        string methodName)
    {
        var method = controllerType.GetMethod(methodName);
        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.DoesNotContain("Customer", attribute!.Roles ?? string.Empty);
    }

    [Fact]
    public void InventoryController_ShouldRequireAuthenticationForReads()
    {
        var controllerAuthorize = typeof(InventoryController)
            .GetCustomAttribute<AuthorizeAttribute>();
        var adjustMethod = typeof(InventoryController)
            .GetMethod(nameof(InventoryController.AdjustStock));

        Assert.NotNull(controllerAuthorize);
        Assert.Null(controllerAuthorize!.Roles);
        Assert.DoesNotContain(
            typeof(InventoryController).GetMethods(),
            method => method.GetCustomAttribute<AllowAnonymousAttribute>() is not null);

        var adjustAuthorize =
            adjustMethod!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(adjustAuthorize);
        Assert.Equal("Staff,Administrator", adjustAuthorize!.Roles);
        Assert.DoesNotContain("Customer", adjustAuthorize.Roles);
    }

    [Fact]
    public async Task ProductsController_GetById_ShouldReturnNotFound()
    {
        var service = new FakeCatalogService();
        var controller = new ProductsController(service);

        var result = await controller.GetProductById(
            Guid.NewGuid(),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Product was not found.", problemDetails.Detail);
    }

    [Fact]
    public async Task ProductsController_Create_ShouldReturnCreatedAtAction()
    {
        var product = ProductResponse();
        var service = new FakeCatalogService
        {
            CreatedProduct = product
        };
        var controller = new ProductsController(service);

        var result = await controller.CreateProduct(
            new ProductCreateDto
            {
                Name = "Test Product",
                CategoryId = Guid.NewGuid(),
                CollectionId = Guid.NewGuid()
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ProductsController.GetProductById), created.ActionName);
        Assert.Same(product, created.Value);
    }

    [Fact]
    public async Task ProductsController_GetVariants_ShouldReturnNotFound_WhenProductMissing()
    {
        var service = new FakeCatalogService();
        var controller = new ProductsController(service);

        var result = await controller.GetProductVariants(
            Guid.NewGuid(),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Product was not found.", problemDetails.Detail);
    }

    [Fact]
    public async Task ProductsController_Delete_ShouldReturnNoContent_WhenDeleted()
    {
        var service = new FakeCatalogService
        {
            DeleteProductResult = true
        };
        var controller = new ProductsController(service);

        var result = await controller.DeleteProduct(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task CategoriesController_Create_ShouldReturnCreatedAtAction()
    {
        var category = new CategoryResponseDto
        {
            Id = Guid.NewGuid(),
            Name = "Tops",
            IsActive = true
        };
        var service = new FakeCatalogService
        {
            CreatedCategory = category
        };
        var controller = new CategoriesController(service);

        var result = await controller.CreateCategory(
            new CategoryCreateDto { Name = "Tops" },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(CategoriesController.GetCategoryById), created.ActionName);
        Assert.Same(category, created.Value);
    }

    [Fact]
    public async Task CollectionsController_Delete_ShouldReturnNotFound_WhenMissing()
    {
        var service = new FakeCatalogService
        {
            DeleteCollectionResult = false
        };
        var controller = new CollectionsController(service);

        var result = await controller.DeleteCollection(
            Guid.NewGuid(),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Collection was not found.", problemDetails.Detail);
    }

    [Fact]
    public async Task ProductsController_CreateVariant_ShouldUseRouteProductId()
    {
        var productId = Guid.NewGuid();
        var variant = new ProductVariantResponseDto
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Sku = "SKU-001",
            Name = "Small / Red"
        };
        var service = new FakeCatalogService
        {
            CreatedVariant = variant
        };
        var controller = new ProductsController(service);
        var request = new ProductVariantCreateDto
        {
            ProductId = Guid.NewGuid(),
            SizeId = Guid.NewGuid(),
            ColourId = Guid.NewGuid(),
            Sku = "SKU-001",
            Name = "Small / Red",
            Price = 100m
        };

        var result = await controller.CreateProductVariant(
            productId,
            request,
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(productId, service.LastVariantCreateRequest?.ProductId);
        Assert.Equal(nameof(VariantsController.GetVariantById), created.ActionName);
        Assert.Equal("Variants", created.ControllerName);
    }

    [Fact]
    public async Task VariantsController_Delete_ShouldReturnNoContent_WhenDeleted()
    {
        var service = new FakeCatalogService
        {
            DeleteVariantResult = true
        };
        var controller = new VariantsController(service);

        var result = await controller.DeleteVariant(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    private static ProductResponseDto ProductResponse() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            CategoryId = Guid.NewGuid(),
            CategoryName = "Category",
            CollectionId = Guid.NewGuid(),
            CollectionName = "Collection",
            IsActive = true
        };

    private sealed class FakeCatalogService : ICatalogService
    {
        public ProductResponseDto? Product { get; set; }
        public ProductResponseDto? CreatedProduct { get; set; }
        public bool DeleteProductResult { get; set; }
        public CategoryResponseDto? CreatedCategory { get; set; }
        public bool DeleteCollectionResult { get; set; }
        public ProductVariantResponseDto? CreatedVariant { get; set; }
        public ProductVariantCreateDto? LastVariantCreateRequest { get; set; }
        public bool DeleteVariantResult { get; set; }

        public Task<List<CategoryResponseDto>> GetCategoriesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<CategoryResponseDto>());

        public Task<CategoryResponseDto?> GetCategoryByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CategoryResponseDto?>(null);

        public Task<CategoryResponseDto> CreateCategoryAsync(
            CategoryCreateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreatedCategory ?? new CategoryResponseDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                IsActive = true
            });

        public Task<CategoryResponseDto?> UpdateCategoryAsync(
            Guid id,
            CategoryUpdateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CategoryResponseDto?>(null);

        public Task<bool> DeleteCategoryAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<List<CollectionResponseDto>> GetCollectionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<CollectionResponseDto>());

        public Task<CollectionResponseDto?> GetCollectionByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CollectionResponseDto?>(null);

        public Task<CollectionResponseDto> CreateCollectionAsync(
            CollectionCreateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CollectionResponseDto
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                IsActive = true
            });

        public Task<CollectionResponseDto?> UpdateCollectionAsync(
            Guid id,
            CollectionUpdateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CollectionResponseDto?>(null);

        public Task<bool> DeleteCollectionAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteCollectionResult);

        public Task<List<SizeResponseDto>> GetSizesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<SizeResponseDto>());

        public Task<SizeResponseDto?> GetSizeByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SizeResponseDto?>(null);

        public Task<SizeResponseDto> CreateSizeAsync(
            SizeCreateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SizeResponseDto());

        public Task<SizeResponseDto?> UpdateSizeAsync(
            Guid id,
            SizeUpdateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SizeResponseDto?>(null);

        public Task<bool> DeleteSizeAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<List<ColourResponseDto>> GetColoursAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ColourResponseDto>());

        public Task<ColourResponseDto?> GetColourByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ColourResponseDto?>(null);

        public Task<ColourResponseDto> CreateColourAsync(
            ColourCreateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ColourResponseDto());

        public Task<ColourResponseDto?> UpdateColourAsync(
            Guid id,
            ColourUpdateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ColourResponseDto?>(null);

        public Task<bool> DeleteColourAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<PagedResponse<ProductResponseDto>> GetProductsAsync(
            ProductQueryDto query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResponse<ProductResponseDto>());

        public Task<ProductResponseDto?> GetProductByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Product);

        public Task<ProductResponseDto> CreateProductAsync(
            ProductCreateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreatedProduct ?? ProductResponse());

        public Task<ProductResponseDto?> UpdateProductAsync(
            Guid id,
            ProductUpdateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductResponseDto?>(null);

        public Task<bool> DeleteProductAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteProductResult);

        public Task<List<ProductVariantResponseDto>> GetVariantsAsync(
            Guid? productId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<ProductVariantResponseDto>());

        public Task<ProductVariantResponseDto?> GetVariantByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductVariantResponseDto?>(null);

        public Task<ProductVariantResponseDto> CreateVariantAsync(
            ProductVariantCreateDto request,
            CancellationToken cancellationToken = default)
        {
            LastVariantCreateRequest = request;
            return Task.FromResult(CreatedVariant ?? new ProductVariantResponseDto());
        }

        public Task<ProductVariantResponseDto?> UpdateVariantAsync(
            Guid id,
            ProductVariantUpdateDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductVariantResponseDto?>(null);

        public Task<bool> DeleteVariantAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DeleteVariantResult);
    }
}
