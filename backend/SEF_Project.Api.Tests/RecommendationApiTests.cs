using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.DTOs.Recommendations;
using SEF_Project.Api.Services.Recommendations;

namespace SEF_Project.Api.Tests;

public class RecommendationApiTests
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
        Assert.Contains(Validate(request), result =>
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
    public async Task RecommendationService_NormalizesInputAndMapsAgentOutput()
    {
        var workflowId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var agent = new RecordingAgent
        {
            Result = new PersonalStylistAgentResult(
                workflowId,
                new RecommendationCustomerContext(9, "Nimal", "Perera"),
                new RecommendationContext(
                    "Wedding",
                    15000m,
                    new[] { "Navy" },
                    "M",
                    "Formal"),
                new[]
                {
                    new PersonalStylistRecommendation(
                        productId,
                        variantId,
                        "Formal Jacket",
                        "Medium",
                        "JACKET-M",
                        12000m,
                        1,
                        3,
                        "M",
                        "Navy",
                        "Available for the requested occasion.")
                },
                new[] { "preferredSize" },
                Array.Empty<string>(),
                new PersonalStylistExecutionSummary(
                    PersonalStylistAgentContract.AgentName,
                    "completed",
                    4,
                    4,
                    true,
                    null,
                    new[]
                    {
                        new RecommendationValidationCheck(
                            "Schema",
                            true,
                            "Valid")
                    }))
        };
        var service = new RecommendationService(agent);

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

        Assert.Equal(17, agent.UserId);
        Assert.Equal("Wedding", agent.Context!.Occasion);
        Assert.Equal(new[] { "Navy" }, agent.Context.PreferredColours);
        Assert.Equal("M", agent.Context.PreferredSize);
        Assert.Equal(workflowId, response.WorkflowId);
        Assert.Equal("Nimal", response.Customer!.FirstName);
        var recommendation = Assert.Single(response.Recommendations);
        Assert.Equal(productId, recommendation.ProductId);
        Assert.Equal(variantId, recommendation.VariantId);
        Assert.Equal(12000m, recommendation.Price);
        Assert.Equal(1, recommendation.Quantity);
        Assert.True(response.Execution.OutputValidated);
        Assert.True(Assert.Single(response.Execution.ValidationResults).IsValid);
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
                WorkflowId = Guid.NewGuid(),
                Status = "completed",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private sealed class RecordingAgent : IPersonalStylistAgent
    {
        public int? UserId { get; private set; }

        public RecommendationContext? Context { get; private set; }

        public PersonalStylistAgentResult Result { get; init; } = null!;

        public Task<PersonalStylistAgentResult> RunAsync(
            int userId,
            RecommendationContext context,
            CancellationToken cancellationToken = default)
        {
            UserId = userId;
            Context = context;
            return Task.FromResult(Result);
        }
    }
}
