using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SEF_Project.Api.AI.InventoryPromotion;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Common;
using SEF_Project.Api.DTOs.Marketing;

namespace SEF_Project.Api.Tests;

/// <summary>
/// Security regression tests for the Marketing &amp; BI component, run through
/// the real pipeline: JWT authentication, role authorization, model
/// validation and the global exception handler.
/// </summary>
public class MarketingSecurityTests : IClassFixture<MarketingApiFactory>
{
    private readonly MarketingApiFactory _factory;

    public MarketingSecurityTests(MarketingApiFactory factory)
    {
        _factory = factory;
    }

    // ---- Authorization matrix --------------------------------------------------

    public enum Access
    {
        /// <summary>Anyone, including anonymous callers.</summary>
        Public,

        /// <summary>Any signed-in user (Customer, Staff, Administrator).</summary>
        SignedIn,

        /// <summary>Staff or Administrator only.</summary>
        StaffOnly
    }

    /// <summary>Every Marketing &amp; BI endpoint and who may call it.</summary>
    public static TheoryData<string, string, Access> Endpoints => new()
    {
        // Promotions: read is public, management is staff, pricing needs a login.
        { "GET", "/api/promotions", Access.Public },
        { "GET", "/api/promotions/{id}", Access.Public },
        { "GET", "/api/promotions/{id}/products", Access.Public },
        { "GET", "/api/promotions/products/{id}", Access.Public },
        { "GET", "/api/promotions/targets", Access.StaffOnly },
        { "POST", "/api/promotions", Access.StaffOnly },
        { "PUT", "/api/promotions/{id}", Access.StaffOnly },
        { "DELETE", "/api/promotions/{id}", Access.StaffOnly },
        { "POST", "/api/promotions/{id}/calculate-discount", Access.SignedIn },

        // Campaigns: staff only.
        { "GET", "/api/campaigns", Access.StaffOnly },
        { "GET", "/api/campaigns/{id}", Access.StaffOnly },
        { "POST", "/api/campaigns", Access.StaffOnly },
        { "PUT", "/api/campaigns/{id}", Access.StaffOnly },
        { "DELETE", "/api/campaigns/{id}", Access.StaffOnly },

        // Business intelligence: staff only.
        { "GET", "/api/analytics/sales/summary", Access.StaffOnly },
        { "GET", "/api/analytics/sales/over-time", Access.StaffOnly },
        { "GET", "/api/analytics/products/performance", Access.StaffOnly },
        { "GET", "/api/analytics/inventory/stock", Access.StaffOnly },
        { "GET", "/api/analytics/inventory/summary", Access.StaffOnly },
        { "GET", "/api/analytics/promotions/performance", Access.StaffOnly },
        { "GET", "/api/analytics/demand", Access.StaffOnly },

        // Agent workflows and approvals: staff only (high impact needs an admin, tested separately).
        { "POST", "/api/agents/inventory-promotion/workflows", Access.StaffOnly },
        { "GET", "/api/agents/inventory-promotion/workflows", Access.StaffOnly },
        { "GET", "/api/agents/inventory-promotion/workflows/{id}", Access.StaffOnly },
        { "POST", "/api/agents/inventory-promotion/workflows/{id}/approve", Access.StaffOnly },
        { "POST", "/api/agents/inventory-promotion/workflows/{id}/reject", Access.StaffOnly },
        { "POST", "/api/agents/inventory-promotion/workflows/{id}/revise", Access.StaffOnly },
    };

    private static async Task<HttpStatusCode> SendAsync(HttpClient client, string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", Guid.NewGuid().ToString()));

        if (method is "POST" or "PUT")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static bool IsAuthFailure(HttpStatusCode status) =>
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_ShouldEnforceAnonymousAccess(string method, string path, Access access)
    {
        var status = await SendAsync(_factory.CreateClientAs(null), method, path);

        if (access == Access.Public)
        {
            Assert.False(IsAuthFailure(status), $"{method} {path} should be public but returned {(int)status}.");
        }
        else
        {
            Assert.Equal(HttpStatusCode.Unauthorized, status);
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_ShouldEnforceCustomerPermissions(string method, string path, Access access)
    {
        var status = await SendAsync(_factory.CreateClientAs("Customer"), method, path);

        if (access == Access.StaffOnly)
        {
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
        else
        {
            Assert.False(IsAuthFailure(status), $"{method} {path} should allow customers but returned {(int)status}.");
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_ShouldAllowStaffAndAdministrators(string method, string path, Access access)
    {
        foreach (var role in new[] { "Staff", "Administrator" })
        {
            var status = await SendAsync(_factory.CreateClientAs(role), method, path);

            Assert.False(IsAuthFailure(status), $"{role} was refused {method} {path} ({(int)status}); access level is {access}.");
        }
    }

    // ---- JWT authentication ----------------------------------------------------

    // Mirrors MarketingApiFactory's test-only JWT settings.
    private const string TestKey = "integration-tests-only-signing-key-0123456789abcdef";
    private const string TestIssuer = "SEF-Project.Api.Tests";
    private const string TestAudience = "SEF-Project.Tests";

    private const string ProtectedPath = "/api/analytics/sales/summary";

    private static string MintToken(
        string key = TestKey,
        string issuer = TestIssuer,
        string audience = TestAudience,
        DateTime? notBefore = null,
        DateTime? expires = null,
        string role = "Administrator",
        bool signed = true)
    {
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, role)
        };

        var credentials = signed
            ? new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
            : null;

        var token = new JwtSecurityToken(
            issuer, audience, claims,
            notBefore ?? now.AddMinutes(-1),
            expires ?? now.AddMinutes(30),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpStatusCode> GetWithBearerAsync(string? token)
    {
        var client = _factory.CreateClient();

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.GetAsync(ProtectedPath);
        return response.StatusCode;
    }

    [Fact]
    public async Task Jwt_ShouldBeAccepted_WhenTokenIsValid()
    {
        Assert.Equal(HttpStatusCode.OK, await GetWithBearerAsync(MintToken()));
    }

    [Fact]
    public async Task Jwt_ShouldBeRejected_WhenTokenIsMalformed()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await GetWithBearerAsync("not-a-jwt"));
    }

    [Fact]
    public async Task Jwt_ShouldBeRejected_WhenTokenIsExpired()
    {
        var token = MintToken(notBefore: DateTime.UtcNow.AddHours(-3), expires: DateTime.UtcNow.AddHours(-2));

        Assert.Equal(HttpStatusCode.Unauthorized, await GetWithBearerAsync(token));
    }

    [Fact]
    public async Task Jwt_ShouldBeRejected_WhenSignedWithAnotherKey()
    {
        var token = MintToken(key: "a-completely-different-signing-key-0123456789abcdef");

        Assert.Equal(HttpStatusCode.Unauthorized, await GetWithBearerAsync(token));
    }

    [Fact]
    public async Task Jwt_ShouldBeRejected_WhenAudienceIsWrong()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await GetWithBearerAsync(MintToken(audience: "someone-else")));
    }

    [Fact]
    public async Task Jwt_ShouldBeRejected_WhenIssuerIsWrong()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await GetWithBearerAsync(MintToken(issuer: "someone-else")));
    }

    [Fact]
    public async Task Jwt_ShouldBeRejected_WhenTokenIsUnsigned()
    {
        // The classic "alg: none" downgrade.
        Assert.Equal(HttpStatusCode.Unauthorized, await GetWithBearerAsync(MintToken(signed: false)));
    }

    [Fact]
    public async Task Jwt_ShouldBeRejected_WhenPayloadWasTamperedWith()
    {
        var parts = MintToken(role: "Customer").Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]))
            .Replace("Customer", "Administrator");
        var forged = $"{parts[0]}.{Base64UrlEncoder.Encode(payload)}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, await GetWithBearerAsync(forged));
    }

    [Fact]
    public async Task Jwt_ShouldBeForbidden_WhenValidTokenLacksTheRequiredRole()
    {
        Assert.Equal(HttpStatusCode.Forbidden, await GetWithBearerAsync(MintToken(role: "Customer")));
    }

    // ---- Pagination and input bounds ---------------------------------------------

    [Theory]
    [InlineData("/api/promotions?page=2147483647&pageSize=100")]
    [InlineData("/api/promotions?page=10001")]
    [InlineData("/api/promotions?page=0")]
    [InlineData("/api/promotions?pageSize=101")]
    [InlineData("/api/promotions?pageSize=0")]
    public async Task PublicPromotionList_ShouldRejectOutOfRangePaging_WithBadRequest(string url)
    {
        using var response = await _factory.CreateClientAs(null).GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/campaigns?page=2147483647&pageSize=100")]
    [InlineData("/api/analytics/products/performance?page=2147483647&pageSize=100")]
    [InlineData("/api/analytics/inventory/stock?page=2147483647&pageSize=100")]
    [InlineData("/api/analytics/promotions/performance?page=2147483647&pageSize=100")]
    [InlineData("/api/analytics/demand?page=2147483647&pageSize=100")]
    [InlineData("/api/analytics/demand?pageSize=101")]
    public async Task StaffLists_ShouldRejectOutOfRangePaging_WithBadRequest(string url)
    {
        using var response = await _factory.CreateClientAs("Staff").GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/promotions?page=10000&pageSize=100")]
    [InlineData("/api/campaigns?page=10000&pageSize=100")]
    [InlineData("/api/analytics/demand?page=10000&pageSize=100")]
    public async Task Lists_ShouldAcceptTheLastAllowedPage(string url)
    {
        using var response = await _factory.CreateClientAs("Staff").GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"\"page\":{PagingLimits.MaxPage}", await response.Content.ReadAsStringAsync());
    }

    private async Task<string> PostPromotionWithTargetsAsync(int productCount)
    {
        var body = JsonSerializer.Serialize(new
        {
            name = "Many targets",
            type = 0, // PercentageDiscount (the API takes the enum numerically)
            discountValue = 10,
            startDate = "2026-11-01T00:00:00Z",
            endDate = "2026-11-30T00:00:00Z",
            productIds = Enumerable.Range(0, productCount).Select(_ => Guid.NewGuid())
        });

        using var response = await _factory.CreateClientAs("Staff").PostAsync(
            "/api/promotions", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task CreatePromotion_ShouldRejectAnUnboundedTargetList_ByModelValidation()
    {
        var problem = await PostPromotionWithTargetsAsync(PromotionRequest.MaxTargets + 1);

        using var document = JsonDocument.Parse(problem);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("ProductIds", out _) || errors.TryGetProperty("productIds", out _), problem);
    }

    [Fact]
    public async Task CreatePromotion_ShouldReachTheService_WhenTargetListIsAtTheLimit()
    {
        // Control: at the limit the request passes model validation and is only
        // refused by the service because these products do not exist.
        var problem = await PostPromotionWithTargetsAsync(PromotionRequest.MaxTargets);

        Assert.Contains("One or more products were not found", problem);
    }

    [Fact]
    public async Task ReviseWorkflow_ShouldRejectAnUnboundedExclusionList()
    {
        var body = JsonSerializer.Serialize(new
        {
            comment = "Exclude a lot of products.",
            excludeProductIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid())
        });

        using var response = await _factory.CreateClientAs("Administrator").PostAsync(
            $"/api/agents/inventory-promotion/workflows/{Guid.NewGuid()}/revise",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Safe error responses -----------------------------------------------------

    [Fact]
    public async Task Errors_ShouldBeProblemDetails_WithoutStackTracesOrInternals()
    {
        using var response = await _factory.CreateClientAs("Staff").PostAsync(
            "/api/promotions", new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();

        // RFC 7807 ProblemDetails shape, not an exception page.
        using var document = JsonDocument.Parse(text);
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.True(document.RootElement.TryGetProperty("title", out _));
        Assert.DoesNotContain(" at SEF_Project", text);
        Assert.DoesNotContain("Exception", text);
        Assert.DoesNotContain("Npgsql", text);
    }

    // ---- Agent security boundaries --------------------------------------------------

    private static readonly Assembly Api = typeof(Program).Assembly;

    private static bool TakesDbContext(Type type) =>
        type.GetConstructors().Any(c => c.GetParameters().Any(p => typeof(DbContext).IsAssignableFrom(p.ParameterType)));

    [Fact]
    public void ProposalModelsAndTools_ShouldNeverReceiveTheDbContext()
    {
        var restricted = Api.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && (typeof(IPromotionAgentTool).IsAssignableFrom(t)
                            || typeof(IPromotionProposalModel).IsAssignableFrom(t)))
            .ToList();

        Assert.NotEmpty(restricted);
        Assert.All(restricted, type =>
            Assert.False(TakesDbContext(type), $"{type.Name} must not take a DbContext: the model and its tools never touch the database directly."));
    }

    [Fact]
    public void OnlyTheOrchestrator_ShouldTakeTheDbContext_InsideTheAgentNamespace()
    {
        var withContext = Api.GetTypes()
            .Where(t => t is { IsClass: true } && t.Namespace == "SEF_Project.Api.AI.InventoryPromotion" && TakesDbContext(t))
            .Select(t => t.Name)
            .ToList();

        // The orchestrator persists the audit trail; nothing else in the agent may.
        Assert.Equal(new[] { nameof(InventoryPromotionAgentService) }, withContext);
    }

    [Fact]
    public void RegisteredTools_ShouldMatchTheAllowList_Exactly()
    {
        using var scope = _factory.Services.CreateScope();
        var registered = scope.ServiceProvider.GetServices<IPromotionAgentTool>().Select(t => t.Name).ToHashSet();

        Assert.True(registered.SetEquals(PromotionAgentConstants.AllowedTools),
            "The tools registered in DI must be exactly the allow-listed tools.");
    }

    [Fact]
    public void EveryToolImplementation_ShouldBeAllowListed()
    {
        var implementations = Api.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPromotionAgentTool).IsAssignableFrom(t))
            .ToList();

        using var scope = _factory.Services.CreateScope();
        var registered = scope.ServiceProvider.GetServices<IPromotionAgentTool>().Select(t => t.GetType()).ToHashSet();

        // A tool class that exists but is not registered would be dead code that
        // could be wired in later without going through the allow-list review.
        Assert.All(implementations, type => Assert.Contains(type, registered));
    }
}
