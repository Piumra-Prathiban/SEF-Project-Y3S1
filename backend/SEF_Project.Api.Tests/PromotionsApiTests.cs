using System.Net;
using System.Net.Http.Json;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Tests;

public class PromotionsApiTests : IClassFixture<MarketingApiFactory>
{
    private const string Url = "/api/promotions";

    private readonly MarketingApiFactory _factory;

    public PromotionsApiTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    private static PromotionRequest ValidRequest(string name = "Test Promotion") =>
        new()
        {
            Name = name,
            Description = "Created by an API test.",
            Type = PromotionType.PercentageDiscount,
            DiscountValue = 15m,
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            CampaignId = SeedData.CampaignSummer,
            ProductIds = new List<Guid> { SeedData.ProductCarbonara }
        };

    private async Task<PromotionResponse> CreateAsStaffAsync(PromotionRequest request)
    {
        var response = await _factory.CreateClientAs("Staff")
            .PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PromotionResponse>())!;
    }

    // ---- Customer-facing reads -------------------------------------------

    [Fact]
    public async Task GetPromotions_ShouldReturnOnlyLivePromotions_WhenAnonymous()
    {
        var response = await _factory.CreateClientAs(null).GetAsync($"{Url}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<PromotionResponse>>();
        var ids = page!.Items.Select(p => p.Id).ToList();

        Assert.Contains(SeedData.PromotionPizza20, ids);
        Assert.Contains(SeedData.PromotionFreeDelivery, ids);
        // 2027 promotion in a Scheduled campaign is hidden from customers.
        Assert.DoesNotContain(SeedData.PromotionColaFixed, ids);
    }

    [Fact]
    public async Task GetPromotions_ShouldReturnAllPromotions_WhenStaff()
    {
        var response = await _factory.CreateClientAs("Staff").GetAsync($"{Url}?pageSize=100");

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<PromotionResponse>>();

        Assert.Contains(page!.Items, p => p.Id == SeedData.PromotionColaFixed);
    }

    [Fact]
    public async Task GetPromotions_ShouldPaginateAndSort_WhenQueryIsProvided()
    {
        var response = await _factory.CreateClientAs("Staff")
            .GetAsync($"{Url}?page=1&pageSize=2&sortBy=name&sortDirection=asc");

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<PromotionResponse>>();

        Assert.Equal(1, page!.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.TotalCount >= 4);
        Assert.True(string.CompareOrdinal(page.Items[0].Name, page.Items[1].Name) <= 0);
    }

    [Fact]
    public async Task GetPromotions_ShouldFilterByType_WhenTypeIsProvided()
    {
        var response = await _factory.CreateClientAs("Staff")
            .GetAsync($"{Url}?type={(int)PromotionType.FixedAmountDiscount}");

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<PromotionResponse>>();

        Assert.NotEmpty(page!.Items);
        Assert.All(page.Items, p => Assert.Equal(PromotionType.FixedAmountDiscount, p.Type));
    }

    [Fact]
    public async Task GetPromotions_ShouldReturnBadRequest_WhenPageSizeIsTooLarge()
    {
        var response = await _factory.CreateClientAs(null).GetAsync($"{Url}?pageSize=500");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPromotionById_ShouldReturnPromotion_WhenLive()
    {
        var response = await _factory.CreateClientAs(null)
            .GetAsync($"{Url}/{SeedData.PromotionPizza20}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var promotion = await response.Content.ReadFromJsonAsync<PromotionResponse>();

        Assert.Equal("Pizza 20% Off", promotion!.Name);
        Assert.Equal("Summer Launch", promotion.CampaignName);
        Assert.Contains(SeedData.ProductMargherita, promotion.ProductIds);
    }

    [Fact]
    public async Task GetPromotionById_ShouldReturnNotFound_WhenNotLiveForCustomer()
    {
        var response = await _factory.CreateClientAs("Customer")
            .GetAsync($"{Url}/{SeedData.PromotionColaFixed}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPromotionById_ShouldReturnNotFound_WhenMissing()
    {
        var response = await _factory.CreateClientAs("Staff")
            .GetAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTargetOptions_ShouldReturnProductsAndCategories_WhenStaff()
    {
        var targets = await _factory.CreateClientAs("Staff")
            .GetFromJsonAsync<PromotionTargetsResponse>($"{Url}/targets");

        Assert.Equal(5, targets!.Products.Count);
        Assert.Equal(4, targets.Categories.Count);
        Assert.Contains(targets.Products, p => p.Id == SeedData.ProductMargherita);
    }

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task GetTargetOptions_ShouldRejectNonStaff(
        string? role,
        HttpStatusCode expected)
    {
        var response = await _factory.CreateClientAs(role).GetAsync($"{Url}/targets");

        Assert.Equal(expected, response.StatusCode);
    }

    // ---- Create / update / delete ----------------------------------------

    [Theory]
    [InlineData("Staff")]
    [InlineData("Administrator")]
    public async Task CreatePromotion_ShouldReturnCreated_WhenStaffOrAdmin(string role)
    {
        var response = await _factory.CreateClientAs(role)
            .PostAsJsonAsync(Url, ValidRequest($"{role} Promotion"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<PromotionResponse>();

        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal($"{role} Promotion", created.Name);
        Assert.Equal(15m, created.DiscountValue);
        Assert.Equal(new[] { SeedData.ProductCarbonara }, created.ProductIds);
    }

    [Fact]
    public async Task CreatePromotion_ShouldReturnUnauthorized_WhenAnonymous()
    {
        var response = await _factory.CreateClientAs(null).PostAsJsonAsync(Url, ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePromotion_ShouldReturnForbidden_WhenCustomer()
    {
        var response = await _factory.CreateClientAs("Customer")
            .PostAsJsonAsync(Url, ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAndDelete_ShouldReturnForbidden_WhenCustomer()
    {
        var client = _factory.CreateClientAs("Customer");

        var update = await client.PutAsJsonAsync(
            $"{Url}/{SeedData.PromotionPizza20}",
            ValidRequest());
        var delete = await client.DeleteAsync($"{Url}/{SeedData.PromotionPizza20}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        var tooLarge = ValidRequest();
        tooLarge.DiscountValue = 150m;
        yield return new object[] { tooLarge };

        var endBeforeStart = ValidRequest();
        endBeforeStart.EndDate = endBeforeStart.StartDate!.Value.AddDays(-1);
        yield return new object[] { endBeforeStart };

        var noName = ValidRequest();
        noName.Name = "";
        yield return new object[] { noName };

        var noTargets = ValidRequest();
        noTargets.ProductIds.Clear();
        yield return new object[] { noTargets };

        var zeroFixed = ValidRequest();
        zeroFixed.Type = PromotionType.FixedAmountDiscount;
        zeroFixed.DiscountValue = 0m;
        yield return new object[] { zeroFixed };

        var missingType = ValidRequest();
        missingType.Type = null;
        yield return new object[] { missingType };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task CreatePromotion_ShouldReturnBadRequest_WhenRequestIsInvalid(
        PromotionRequest request)
    {
        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePromotion_ShouldReturnBadRequest_WhenCampaignDoesNotExist()
    {
        var request = ValidRequest();
        request.CampaignId = Guid.NewGuid();

        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePromotion_ShouldReturnBadRequest_WhenProductDoesNotExist()
    {
        var request = ValidRequest();
        request.ProductIds = new List<Guid> { Guid.NewGuid() };

        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePromotion_ShouldReturnConflict_WhenOutsideCampaignDates()
    {
        var request = ValidRequest();
        request.EndDate = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var response = await _factory.CreateClientAs("Staff").PostAsJsonAsync(Url, request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePromotion_ShouldReturnUpdatedPromotion_WhenStaff()
    {
        var created = await CreateAsStaffAsync(ValidRequest("Before Update"));

        var update = ValidRequest("After Update");
        update.DiscountValue = 25m;
        update.ProductIds = new List<Guid> { SeedData.ProductCarbonara, SeedData.ProductTiramisu };

        var response = await _factory.CreateClientAs("Staff")
            .PutAsJsonAsync($"{Url}/{created.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<PromotionResponse>();

        Assert.Equal("After Update", updated!.Name);
        Assert.Equal(25m, updated.DiscountValue);
        Assert.Equal(2, updated.ProductIds.Count);
    }

    [Fact]
    public async Task UpdatePromotion_ShouldReturnNotFound_WhenMissing()
    {
        var response = await _factory.CreateClientAs("Staff")
            .PutAsJsonAsync($"{Url}/{Guid.NewGuid()}", ValidRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletePromotion_ShouldReturnNoContent_ThenNotFound()
    {
        var created = await CreateAsStaffAsync(ValidRequest("To Delete"));
        var client = _factory.CreateClientAs("Administrator");

        var delete = await client.DeleteAsync($"{Url}/{created.Id}");
        var get = await client.GetAsync($"{Url}/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeletePromotion_ShouldReturnNotFound_WhenMissing()
    {
        var response = await _factory.CreateClientAs("Staff").DeleteAsync($"{Url}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletePromotion_ShouldReturnConflict_WhenPromotionHasCoupons()
    {
        // Seeded SUMMER20 coupon belongs to "Pizza 20% Off".
        var response = await _factory.CreateClientAs("Staff")
            .DeleteAsync($"{Url}/{SeedData.PromotionPizza20}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ---- Business operation ------------------------------------------------

    [Fact]
    public async Task CalculateDiscount_ShouldReturnDiscount_WhenCustomer()
    {
        var response = await _factory.CreateClientAs("Customer").PostAsJsonAsync(
            $"{Url}/{SeedData.PromotionPizza20}/calculate-discount",
            new CalculatePromotionDiscountRequest
            {
                ProductVariantId = SeedData.VariantMargheritaSmall
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PromotionDiscountResponse>();

        Assert.Equal(SeedData.PromotionPizza20, result!.PromotionId);
        Assert.Equal(SeedData.VariantMargheritaSmall, result.ProductVariantId);
        Assert.Equal(1200m, result.OriginalPrice);
        Assert.Equal(240m, result.DiscountAmount);
        Assert.Equal(960m, result.FinalPrice);
    }

    [Fact]
    public async Task CalculateDiscount_ShouldIgnoreClientSuppliedPrice()
    {
        var response = await _factory.CreateClientAs("Customer").PostAsJsonAsync(
            $"{Url}/{SeedData.PromotionPizza20}/calculate-discount",
            new
            {
                productVariantId = SeedData.VariantMargheritaSmall,
                price = 1m,
                discountAmount = 1000m
            });

        var result = await response.Content.ReadFromJsonAsync<PromotionDiscountResponse>();

        Assert.Equal(1200m, result!.OriginalPrice);
        Assert.Equal(960m, result.FinalPrice);
    }

    [Fact]
    public async Task CalculateDiscount_ShouldReturnConflict_WhenProductIsNotIncluded()
    {
        var response = await _factory.CreateClientAs("Customer").PostAsJsonAsync(
            $"{Url}/{SeedData.PromotionPizza20}/calculate-discount",
            new CalculatePromotionDiscountRequest
            {
                ProductVariantId = SeedData.VariantCola330
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CalculateDiscount_ShouldReturnNotFound_WhenPromotionIsMissing()
    {
        var response = await _factory.CreateClientAs("Customer").PostAsJsonAsync(
            $"{Url}/{Guid.NewGuid()}/calculate-discount",
            new CalculatePromotionDiscountRequest
            {
                ProductVariantId = SeedData.VariantMargheritaSmall
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CalculateDiscount_ShouldReturnBadRequest_WhenVariantIdIsEmpty()
    {
        var response = await _factory.CreateClientAs("Customer").PostAsJsonAsync(
            $"{Url}/{SeedData.PromotionPizza20}/calculate-discount",
            new CalculatePromotionDiscountRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CalculateDiscount_ShouldReturnUnauthorized_WhenAnonymous()
    {
        var response = await _factory.CreateClientAs(null).PostAsJsonAsync(
            $"{Url}/{SeedData.PromotionPizza20}/calculate-discount",
            new CalculatePromotionDiscountRequest
            {
                ProductVariantId = SeedData.VariantMargheritaSmall
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
