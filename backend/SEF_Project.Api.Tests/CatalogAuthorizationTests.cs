using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using SEF_Project.Api.Controllers;

namespace SEF_Project.Api.Tests;

public class CatalogAuthorizationTests
{
    [Fact]
    public void InventoryController_ShouldRequireStaffOrAdministratorRole()
    {
        var attribute = typeof(InventoryController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Staff,Administrator", attribute!.Roles);
    }

    [Theory]
    [InlineData(nameof(CategoriesController.CreateCategory))]
    [InlineData(nameof(CategoriesController.UpdateCategory))]
    [InlineData(nameof(CategoriesController.DeleteCategory))]
    public void CategoryModificationEndpoints_ShouldRequireStaffOrAdministratorRole(string methodName)
    {
        var method = typeof(CategoriesController).GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("Staff,Administrator", attribute!.Roles);
    }

    [Theory]
    [InlineData(nameof(ProductsController.CreateProduct))]
    [InlineData(nameof(ProductsController.UpdateProduct))]
    [InlineData(nameof(ProductsController.DeleteProduct))]
    [InlineData(nameof(ProductsController.CreateVariant))]
    [InlineData(nameof(ProductsController.UpdateVariant))]
    [InlineData(nameof(ProductsController.DeleteVariant))]
    [InlineData(nameof(ProductsController.AddImage))]
    [InlineData(nameof(ProductsController.UpdateImage))]
    [InlineData(nameof(ProductsController.DeleteImage))]
    [InlineData(nameof(ProductsController.SetPrimaryImage))]
    public void ProductModificationEndpoints_ShouldRequireStaffOrAdministratorRole(string methodName)
    {
        var method = typeof(ProductsController).GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("Staff,Administrator", attribute!.Roles);
    }

    [Theory]
    [InlineData(nameof(CategoriesController.GetCategories))]
    [InlineData(nameof(CategoriesController.GetCategoryById))]
    public void CategoryReadEndpoints_ShouldAllowPublicAccess(string methodName)
    {
        var method = typeof(CategoriesController).GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Null(attribute);
    }

    [Theory]
    [InlineData(nameof(ProductsController.GetProducts))]
    [InlineData(nameof(ProductsController.GetProductById))]
    public void ProductReadEndpoints_ShouldAllowPublicAccess(string methodName)
    {
        var method = typeof(ProductsController).GetMethod(methodName);
        Assert.NotNull(method);

        var attribute = method!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Null(attribute);
    }
}
