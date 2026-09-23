using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Auth;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Tests;

public class RequestValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            model,
            context,
            results,
            validateAllProperties: true);

        return results;
    }

    [Fact]
    public void RegisterRequest_ShouldBeValid_WhenAllFieldsAreCorrect()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "TestPassword123!",
            FirstName = "Test",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void RegisterRequest_ShouldBeInvalid_WhenEmailIsInvalid()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "not-an-email",
            Password = "TestPassword123!",
            FirstName = "Test",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(RegisterRequest.Email)));
    }

    [Fact]
    public void RegisterRequest_ShouldBeInvalid_WhenPasswordIsTooShort()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "short",
            FirstName = "Test",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(RegisterRequest.Password)));
    }

    [Fact]
    public void RegisterRequest_ShouldBeInvalid_WhenFirstNameIsMissing()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "TestPassword123!",
            FirstName = "",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(RegisterRequest.FirstName)));
    }

    [Fact]
    public void LoginRequest_ShouldBeValid_WhenCredentialsAreCorrect()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "TestPassword123!"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void LoginRequest_ShouldBeInvalid_WhenEmailIsInvalid()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "invalid-email",
            Password = "TestPassword123!"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(LoginRequest.Email)));
    }

    [Fact]
    public void LoginRequest_ShouldBeInvalid_WhenPasswordIsMissing()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = ""
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(LoginRequest.Password)));
    }

    [Fact]
    public void ProductCreateDto_ShouldBeInvalid_WhenRequiredFieldsAreMissing()
    {
        var request = new ProductCreateDto
        {
            Name = "",
            CategoryId = Guid.Empty,
            CollectionId = Guid.Empty
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ProductCreateDto.Name)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ProductCreateDto.CategoryId)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ProductCreateDto.CollectionId)));
    }

    [Fact]
    public void CategoryAndCollectionDtos_ShouldRejectMissingNames()
    {
        var categoryResults = Validate(new CategoryCreateDto { Name = "" });
        var collectionResults = Validate(new CollectionCreateDto { Name = "" });

        Assert.Contains(
            categoryResults,
            result => result.MemberNames.Contains(nameof(CategoryCreateDto.Name)));
        Assert.Contains(
            collectionResults,
            result => result.MemberNames.Contains(nameof(CollectionCreateDto.Name)));
    }

    [Fact]
    public void SizeCreateDto_ShouldRejectMissingNameAndNegativeDisplayOrder()
    {
        var request = new SizeCreateDto
        {
            Name = "",
            DisplayOrder = -1
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(SizeCreateDto.Name)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(SizeCreateDto.DisplayOrder)));
    }

    [Fact]
    public void ColourCreateDto_ShouldRejectMissingNameAndInvalidHexCode()
    {
        var request = new ColourCreateDto
        {
            Name = "",
            HexCode = "red"
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ColourCreateDto.Name)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ColourCreateDto.HexCode)));
    }

    [Fact]
    public void ProductVariantCreateDto_ShouldAllowZeroPrice()
    {
        var request = new ProductVariantCreateDto
        {
            ProductId = Guid.NewGuid(),
            SizeId = Guid.NewGuid(),
            ColourId = Guid.NewGuid(),
            Sku = "ZERO-PRICE",
            Name = "Zero Price Variant",
            Price = 0m
        };

        var results = Validate(request);

        Assert.DoesNotContain(
            results,
            result => result.MemberNames.Contains(nameof(ProductVariantCreateDto.Price)));
    }

    [Fact]
    public void ProductVariantCreateDto_ShouldRejectNegativePriceAndStock()
    {
        var request = new ProductVariantCreateDto
        {
            ProductId = Guid.NewGuid(),
            SizeId = Guid.NewGuid(),
            ColourId = Guid.NewGuid(),
            Sku = "NEGATIVE-PRICE",
            Name = "Negative Price Variant",
            Price = -1m,
            InitialQuantityOnHand = -1,
            ReorderLevel = -1
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ProductVariantCreateDto.Price)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ProductVariantCreateDto.InitialQuantityOnHand)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(ProductVariantCreateDto.ReorderLevel)));
    }

    [Fact]
    public void StockAdjustmentDto_ShouldRejectInvalidQuantityAndTransactionType()
    {
        var request = new StockAdjustmentDto
        {
            ProductVariantId = Guid.NewGuid(),
            Type = (InventoryTransactionType)999,
            Quantity = 0,
            Reason = ""
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(StockAdjustmentDto.Type)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(StockAdjustmentDto.Quantity)));
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(StockAdjustmentDto.Reason)));
    }
}
