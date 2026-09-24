using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Tests;

public class PersonalStylistValidationTests
{
    private readonly PersonalStylistOutputValidator _validator = new();

    [Fact]
    public void ValidRecommendation_PassesEveryDeterministicCheck()
    {
        var fixture = ValidationFixture.Create();

        var result = _validator.Validate(
            new PersonalStylistModelOutput(new[] { fixture.Draft }),
            fixture.Input);

        Assert.True(result.IsValid);
        Assert.Equal(11, result.Checks.Count);
        Assert.All(result.Checks, check => Assert.True(check.IsValid));
        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal(fixture.Availability.Price, recommendation.Price);
        Assert.Equal(fixture.Draft.Quantity, recommendation.Quantity);
    }

    [Fact]
    public void NonexistentProduct_IsRejected()
    {
        var fixture = ValidationFixture.Create();

        var result = Validate(fixture, fixture.Draft with
        {
            ProductId = Guid.NewGuid()
        });

        AssertInvalid(result, "ProductExists");
    }

    [Fact]
    public void NonexistentVariant_IsRejected()
    {
        var fixture = ValidationFixture.Create();

        var result = Validate(fixture, fixture.Draft with
        {
            VariantId = Guid.NewGuid()
        });

        AssertInvalid(result, "VariantExists");
    }

    [Fact]
    public void VariantBelongingToAnotherProduct_IsRejected()
    {
        var fixture = ValidationFixture.Create();
        var otherProductId = Guid.NewGuid();
        var otherProduct = fixture.Product with
        {
            ProductId = otherProductId,
            Name = "Other product"
        };
        var input = fixture.Input with
        {
            CatalogueProducts = new[] { fixture.Product, otherProduct }
        };

        var result = _validator.Validate(
            new PersonalStylistModelOutput(new[]
            {
                fixture.Draft with { ProductId = otherProductId }
            }),
            input);

        AssertInvalid(result, "VariantOwnership");
    }

    [Fact]
    public void UnavailableSize_IsRejected()
    {
        var fixture = ValidationFixture.Create();

        var result = Validate(fixture, fixture.Draft with { Size = "L" });

        AssertInvalid(result, "Size");
    }

    [Fact]
    public void UnavailableColour_IsRejected()
    {
        var fixture = ValidationFixture.Create();

        var result = Validate(fixture, fixture.Draft with { Colour = "Red" });

        AssertInvalid(result, "Colour");
    }

    [Fact]
    public void InsufficientStock_IsRejected()
    {
        var fixture = ValidationFixture.Create();

        var result = Validate(fixture, fixture.Draft with
        {
            Quantity = fixture.Availability.AvailableQuantity + 1
        });

        AssertInvalid(result, "Stock");
    }

    [Fact]
    public void UnavailableVariant_IsRejected()
    {
        var fixture = ValidationFixture.Create();
        var input = fixture.Input with
        {
            AvailableVariants = Array.Empty<VerifiedProductAvailability>()
        };

        var result = _validator.Validate(
            new PersonalStylistModelOutput(new[] { fixture.Draft }),
            input);

        AssertInvalid(result, "Availability");
    }

    [Fact]
    public void IncorrectPrice_IsRejectedWithoutRepairingOutput()
    {
        var fixture = ValidationFixture.Create();

        var result = Validate(fixture, fixture.Draft with
        {
            Price = fixture.Availability.Price - 1
        });

        AssertInvalid(result, "Price");
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void TotalBudgetExceeded_IsRejected()
    {
        var fixture = ValidationFixture.Create();
        var secondProductId = Guid.NewGuid();
        var secondVariantId = Guid.NewGuid();
        var secondVariant = fixture.Product.AvailableVariants[0] with
        {
            Id = secondVariantId,
            Sku = "JACKET-M-NAVY-2"
        };
        var secondProduct = fixture.Product with
        {
            ProductId = secondProductId,
            Name = "Second Formal Jacket",
            AvailableVariants = new[] { secondVariant }
        };
        var secondAvailability = fixture.Availability with
        {
            ProductId = secondProductId,
            VariantId = secondVariantId,
            ProductName = secondProduct.Name,
            Sku = secondVariant.Sku
        };
        var secondDraft = fixture.Draft with
        {
            ProductId = secondProductId,
            VariantId = secondVariantId
        };
        var input = fixture.Input with
        {
            Preferences = fixture.Input.Preferences with { Budget = 150m },
            CatalogueProducts = new[] { fixture.Product, secondProduct },
            AvailableVariants = new[]
            {
                fixture.Availability,
                secondAvailability
            }
        };

        var result = _validator.Validate(
            new PersonalStylistModelOutput(new[]
            {
                fixture.Draft,
                secondDraft
            }),
            input);

        AssertInvalid(result, "Budget");
    }

    [Fact]
    public void MalformedAiResponse_IsRejected()
    {
        var fixture = ValidationFixture.Create();

        var result = _validator.Validate(
            new PersonalStylistModelOutput(null!),
            fixture.Input);

        AssertInvalid(result, "Schema");
    }

    private AgentOutputValidation Validate(
        ValidationFixture fixture,
        PersonalStylistDraftRecommendation draft) =>
        _validator.Validate(
            new PersonalStylistModelOutput(new[] { draft }),
            fixture.Input);

    private static void AssertInvalid(
        AgentOutputValidation result,
        string rule)
    {
        Assert.False(result.IsValid);
        Assert.Empty(result.Recommendations);
        Assert.Contains(result.Checks, check =>
            check.Rule == rule && !check.IsValid);
    }

    private sealed record ValidationFixture(
        RecommendationCatalogProduct Product,
        VerifiedProductAvailability Availability,
        PersonalStylistDraftRecommendation Draft,
        PersonalStylistModelInput Input)
    {
        public static ValidationFixture Create()
        {
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            var variant = new RecommendationCatalogVariant(
                variantId,
                "JACKET-M-NAVY",
                "Medium / Navy",
                100m,
                5,
                "M",
                "Navy");
            var product = new RecommendationCatalogProduct(
                productId,
                "Formal Jacket",
                "Tailored jacket",
                100m,
                Array.Empty<RecommendationCatalogCategory>(),
                new[] { variant });
            var availability = new VerifiedProductAvailability(
                productId,
                variantId,
                product.Name,
                variant.Name,
                variant.Sku,
                variant.Price,
                variant.AvailableQuantity,
                variant.Size,
                variant.Colour);
            var context = new RecommendationContext(
                "Wedding",
                200m,
                new[] { "Navy" },
                "M",
                "Classic");
            var draft = new PersonalStylistDraftRecommendation(
                productId,
                variantId,
                100m,
                1,
                "M",
                "Navy",
                "Suitable for the requested occasion.");
            var input = new PersonalStylistModelInput(
                new RecommendationCustomerContext(1, "Test", "Customer"),
                context,
                Array.Empty<WishlistToolItem>(),
                new[] { product },
                new[] { availability },
                Array.Empty<string>());

            return new ValidationFixture(product, availability, draft, input);
        }
    }
}
