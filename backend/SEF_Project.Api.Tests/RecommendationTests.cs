using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.DTOs.Recommendations;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Recommendations;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Tests;

public class RecommendationTests
{
    [Fact]
    public void RecommendationRequest_IsValidForFashionPreferences()
    {
        var request = new RecommendationRequest
        {
            Occasion = "Wedding",
            Budget = 25000m,
            PreferredColours = new[] { "Navy", "Silver" },
            PreferredSize = "M",
            StylePreferences = "Formal and minimal"
        };

        Assert.Empty(Validate(request));
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void RecommendationRequest_RejectsInvalidInput(
        RecommendationRequest request,
        string expectedMember)
    {
        var results = Validate(request);

        Assert.Contains(results, result =>
            result.MemberNames.Contains(expectedMember));
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return new object[]
        {
            new RecommendationRequest { Occasion = "" },
            nameof(RecommendationRequest.Occasion)
        };
        yield return new object[]
        {
            new RecommendationRequest { Occasion = "Work", Budget = 0 },
            nameof(RecommendationRequest.Budget)
        };
        yield return new object[]
        {
            new RecommendationRequest
            {
                Occasion = "Work",
                PreferredColours = new[] { "Navy", " " }
            },
            nameof(RecommendationRequest.PreferredColours)
        };
        yield return new object[]
        {
            new RecommendationRequest
            {
                Occasion = "Work",
                PreferredColours = Enumerable.Range(1, 11)
                    .Select(index => $"Colour {index}")
                    .ToArray()
            },
            nameof(RecommendationRequest.PreferredColours)
        };
        yield return new object[]
        {
            new RecommendationRequest
            {
                Occasion = "Work",
                PreferredSize = new string('X', 51)
            },
            nameof(RecommendationRequest.PreferredSize)
        };
    }

    [Fact]
    public void RecommendationsController_RequiresAuthentication()
    {
        Assert.NotNull(typeof(RecommendationsController)
            .GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Start_ReturnsUnauthorizedWithoutUserClaim()
    {
        var controller = CreateController(new RecordingRecommendationService());

        var result = await controller.Start(
            new RecommendationRequest { Occasion = "Work" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Start_UsesAuthenticatedClaimAsCustomerIdentity()
    {
        var service = new RecordingRecommendationService();
        var controller = CreateController(service, userId: 42);

        var result = await controller.Start(
            new RecommendationRequest { Occasion = "Dinner" },
            CancellationToken.None);

        Assert.Equal(42, service.UserId);
        Assert.IsType<RecommendationResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    [Fact]
    public async Task RecommendationService_NormalizesPreferencesAndMapsToolData()
    {
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var orchestrator = new RecordingOrchestrator
        {
            Result = new RecommendationOrchestrationResult(
                new RecommendationCustomerContext(9, "Nimal", "Perera"),
                new[]
                {
                    new RecommendationCatalogProduct(
                        productId,
                        "Formal Jacket",
                        "Tailored jacket",
                        12000m,
                        Array.Empty<RecommendationCatalogCategory>(),
                        new[]
                        {
                            new RecommendationCatalogVariant(
                                variantId,
                                "JACKET-M",
                                "Medium",
                                12000m,
                                3)
                        })
                },
                new[] { "preferredSize" },
                Array.Empty<string>())
        };
        var service = new RecommendationService(orchestrator);

        var response = await service.StartAsync(
            17,
            new RecommendationRequest
            {
                Occasion = "  Wedding  ",
                Budget = 15000m,
                PreferredColours = new[] { " Navy ", "navy" },
                PreferredSize = " M ",
                StylePreferences = " Formal "
            });

        Assert.Equal(17, orchestrator.UserId);
        Assert.Equal("Wedding", orchestrator.Context!.Occasion);
        Assert.Equal(new[] { "Navy" }, orchestrator.Context.PreferredColours);
        Assert.Equal("M", orchestrator.Context.PreferredSize);
        Assert.Equal("Nimal", response.Customer.FirstName);
        Assert.Equal(productId, Assert.Single(response.Products).ProductId);
        Assert.Equal(variantId, Assert.Single(response.Products[0].AvailableVariants).Id);
        Assert.NotEqual(Guid.Empty, response.RequestId);
        Assert.NotEqual(default, response.CreatedAt);
    }

    [Fact]
    public async Task Orchestrator_UsesControlledToolsAndReportsUnsupportedPreferences()
    {
        var product = CatalogProduct();
        var customerTool = new RecordingCustomerTool();
        var catalogTool = new RecordingCatalogTool(
            Array.Empty<RecommendationCatalogProduct>(),
            new[] { product });
        var orchestrator = new CatalogRecommendationOrchestrator(
            customerTool,
            catalogTool);

        var result = await orchestrator.CreateCandidatesAsync(
            7,
            new RecommendationContext(
                "Wedding",
                20000m,
                new[] { "Blue" },
                "M",
                "Classic"));

        Assert.Equal(7, customerTool.UserId);
        Assert.Equal(2, catalogTool.Queries.Count);
        Assert.Equal("Wedding", catalogTool.Queries[0].SearchText);
        Assert.Null(catalogTool.Queries[1].SearchText);
        Assert.Equal(20000m, catalogTool.Queries[1].MaximumPrice);
        Assert.Equal(product.ProductId, Assert.Single(result.Products).ProductId);
        Assert.Equal(
            new[] { "preferredColours", "preferredSize", "stylePreferences" },
            result.UnappliedPreferences);
        Assert.Equal(new[] { "occasion" }, result.RelaxedCriteria);
    }

    [Fact]
    public async Task CatalogTool_UsesAuthoritativeSearchPriceAndAvailability()
    {
        var productId = Guid.NewGuid();
        var availableVariant = Guid.NewGuid();
        var productSearch = new RecordingProductSearchService
        {
            Response = new PagedProductResponse
            {
                Items = new[]
                {
                    new ShoppingProductResponse
                    {
                        Id = productId,
                        Name = "Work Shirt",
                        IsAvailable = true,
                        Variants = new[]
                        {
                            new ShoppingProductVariantResponse
                            {
                                Id = Guid.NewGuid(),
                                Sku = "NO-STOCK",
                                Name = "No stock",
                                Price = 50m,
                                AvailableQuantity = 0,
                                IsAvailable = false
                            },
                            new ShoppingProductVariantResponse
                            {
                                Id = availableVariant,
                                Sku = "AVAILABLE",
                                Name = "Available",
                                Price = 100m,
                                AvailableQuantity = 2,
                                IsAvailable = true
                            },
                            new ShoppingProductVariantResponse
                            {
                                Id = Guid.NewGuid(),
                                Sku = "OVER-BUDGET",
                                Name = "Over budget",
                                Price = 500m,
                                AvailableQuantity = 5,
                                IsAvailable = true
                            }
                        }
                    }
                },
                Page = 1,
                PageSize = 12,
                TotalCount = 1,
                TotalPages = 1
            }
        };
        var tool = new RecommendationCatalogTool(productSearch);

        var products = await tool.SearchAsync(
            new RecommendationCatalogQuery("Work", 200m));

        Assert.Equal("Work", productSearch.Query!.Search);
        Assert.Equal(200m, productSearch.Query.MaxPrice);
        Assert.True(productSearch.Query.InStockOnly);
        Assert.Equal("price", productSearch.Query.SortBy);
        var product = Assert.Single(products);
        Assert.Equal(100m, product.MinimumAvailablePrice);
        Assert.Equal(
            availableVariant,
            Assert.Single(product.AvailableVariants).Id);
    }

    private static IList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }

    private static RecommendationCatalogProduct CatalogProduct() => new(
        Guid.NewGuid(),
        "Evening Dress",
        null,
        18000m,
        Array.Empty<RecommendationCatalogCategory>(),
        new[]
        {
            new RecommendationCatalogVariant(
                Guid.NewGuid(),
                "DRESS-M",
                "Medium",
                18000m,
                2)
        });

    private static RecommendationsController CreateController(
        IRecommendationService service,
        int? userId = null)
    {
        var claims = userId.HasValue
            ? new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId.Value.ToString())
            }
            : Array.Empty<Claim>();
        var identity = new ClaimsIdentity(
            claims,
            userId.HasValue ? "TestAuthentication" : null);

        return new RecommendationsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private sealed class RecordingRecommendationService : IRecommendationService
    {
        public int? UserId { get; private set; }

        public Task<RecommendationResponse> StartAsync(
            int userId,
            RecommendationRequest request,
            CancellationToken cancellationToken = default)
        {
            UserId = userId;
            return Task.FromResult(new RecommendationResponse
            {
                RequestId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private sealed class RecordingOrchestrator : IRecommendationOrchestrator
    {
        public int? UserId { get; private set; }

        public RecommendationContext? Context { get; private set; }

        public RecommendationOrchestrationResult Result { get; init; } = null!;

        public Task<RecommendationOrchestrationResult> CreateCandidatesAsync(
            int userId,
            RecommendationContext context,
            CancellationToken cancellationToken = default)
        {
            UserId = userId;
            Context = context;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingCustomerTool : IRecommendationCustomerTool
    {
        public int? UserId { get; private set; }

        public Task<RecommendationCustomerContext> GetCurrentCustomerAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            UserId = userId;
            return Task.FromResult(
                new RecommendationCustomerContext(3, "Test", "Customer"));
        }
    }

    private sealed class RecordingCatalogTool : IRecommendationCatalogTool
    {
        private readonly Queue<IReadOnlyList<RecommendationCatalogProduct>> _results;

        public RecordingCatalogTool(
            params IReadOnlyList<RecommendationCatalogProduct>[] results)
        {
            _results = new Queue<IReadOnlyList<RecommendationCatalogProduct>>(results);
        }

        public RecommendationCatalogCapabilities Capabilities { get; } =
            new(SupportsColour: false, SupportsSize: false);

        public List<RecommendationCatalogQuery> Queries { get; } = new();

        public Task<IReadOnlyList<RecommendationCatalogProduct>> SearchAsync(
            RecommendationCatalogQuery query,
            CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class RecordingProductSearchService : IProductSearchService
    {
        public ProductSearchQuery? Query { get; private set; }

        public PagedProductResponse Response { get; init; } = null!;

        public Task<PagedProductResponse> SearchAsync(
            ProductSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(Response);
        }
    }
}
