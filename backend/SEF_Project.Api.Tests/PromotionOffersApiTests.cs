using System.Net;
using System.Net.Http.Json;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Marketing;

namespace SEF_Project.Api.Tests;

// Read-only customer endpoints; the fixed clock (2026-10-15) makes the seeded
// "Tops 20% Off", "Footwear Week 10% Off" and "Free Delivery" promotions live.
public class PromotionOffersApiTests : IClassFixture<MarketingApiFactory>
{
    private readonly MarketingApiFactory _factory;

    public PromotionOffersApiTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetPromotionProducts_ShouldReturnServerPricedVariants_WhenAnonymous()
    {
        var response = await _factory.CreateClientAs(null)
            .GetFromJsonAsync<PromotionProductsResponse>(
                $"/api/promotions/{SeedData.PromotionTops20}/products");

        Assert.True(response!.HasPriceDiscount);
        Assert.Equal(
            new[] { "Classic Cotton T-Shirt", "Fleece Pullover Hoodie" },
            response.Products.Select(p => p.ProductName));

        var small = response.Products[0].Variants.Single(v => v.Sku == "TSH-CLS-XS");
        Assert.Equal(2500m, small.OriginalPrice);
        Assert.Equal(500m, small.DiscountAmount);
        Assert.Equal(2000m, small.FinalPrice);
        Assert.Equal(SeedData.PromotionTops20, small.PromotionId);
        Assert.Equal("LKR", small.Currency);
    }

    [Fact]
    public async Task GetPromotionProducts_ShouldIncludeCategoryTargetedProducts()
    {
        var response = await _factory.CreateClientAs(null)
            .GetFromJsonAsync<PromotionProductsResponse>(
                $"/api/promotions/{SeedData.PromotionFootwear10}/products");

        var boots = Assert.Single(response!.Products);
        Assert.Equal("Leather Ankle Boots", boots.ProductName);
        Assert.Equal(8010m, Assert.Single(boots.Variants).FinalPrice);
    }

    [Fact]
    public async Task GetPromotionProducts_ShouldNotDiscount_WhenPromotionIsNotAPriceDiscount()
    {
        var response = await _factory.CreateClientAs(null)
            .GetFromJsonAsync<PromotionProductsResponse>(
                $"/api/promotions/{SeedData.PromotionFreeDelivery}/products");

        Assert.False(response!.HasPriceDiscount);
        Assert.Empty(response.Products);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000055")] // Denim promotion: 2027, Scheduled campaign
    [InlineData("11111111-1111-1111-1111-111111111111")] // missing
    public async Task GetPromotionProducts_ShouldReturnNotFound_WhenNotLiveOrMissing(string id)
    {
        var response = await _factory.CreateClientAs(null).GetAsync($"/api/promotions/{id}/products");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProductPromotions_ShouldReturnBestPricePerVariant()
    {
        var response = await _factory.CreateClientAs(null)
            .GetFromJsonAsync<ProductPromotionsResponse>(
                $"/api/promotions/products/{SeedData.ProductHoodie}");

        Assert.True(response!.HasActivePromotion);
        Assert.Equal("Tops 20% Off", Assert.Single(response.Promotions).Name);
        Assert.Equal(new[] { 5200m, 5520m }, response.Variants.Select(v => v.FinalPrice));
        Assert.All(response.Variants, v => Assert.Equal("Tops 20% Off", v.PromotionName));
    }

    [Fact]
    public async Task GetProductPromotions_ShouldReturnFullPrice_WhenNoPromotionApplies()
    {
        var response = await _factory.CreateClientAs(null)
            .GetFromJsonAsync<ProductPromotionsResponse>(
                $"/api/promotions/products/{SeedData.ProductJacket}");

        Assert.False(response!.HasActivePromotion);
        Assert.Empty(response.Promotions);

        var variant = Assert.Single(response.Variants);
        Assert.Equal(12500m, variant.FinalPrice);
        Assert.Equal(0m, variant.DiscountAmount);
        Assert.Null(variant.PromotionId);
    }

    [Fact]
    public async Task GetProductPromotions_ShouldReturnNotFound_WhenProductIsMissing()
    {
        var response = await _factory.CreateClientAs(null)
            .GetAsync($"/api/promotions/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
