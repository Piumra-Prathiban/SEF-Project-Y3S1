using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Configuration;
using SEF_Project.Api.Data;
using SEF_Project.Api.Models;
using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Tests;

/// <summary>
/// Hosts the real API pipeline (routing, JWT auth, role authorization, model
/// validation, GlobalExceptionHandler) over an in-memory SQLite database with
/// the seed data and a fixed clock inside the seeded promotion windows.
/// </summary>
public class MarketingApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTime Now =
        new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    // Test-only values; never used outside this test host.
    private static readonly JwtSettings TestJwtSettings = new()
    {
        Key = "integration-tests-only-signing-key-0123456789abcdef",
        Issuer = "SEF-Project.Api.Tests",
        Audience = "SEF-Project.Tests",
        ExpiryMinutes = 30
    };

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly MutableTimeProvider _timeProvider = new(Now);

    // A real, persisted User per role, keyed by role name. CreateClientAs's
    // tokens carry these ids (never a made-up one), so any code path that
    // saves "who did this" as a foreign key (e.g. AgentApproval.
    // ReviewedByUserId) finds a row that actually exists -- exactly like a
    // real login always would.
    private readonly Dictionary<string, int> _fixtureUserIds = new();

    public MarketingApiFactory()
    {
        _connection.Open();
    }

    /// <summary>
    /// Moves the shared test clock forward (e.g. so a promotion scheduled to
    /// start "tomorrow" becomes live, the same way it would after a real day
    /// passes in production). Every request in this test host reads time
    /// through this one instance, so the change is visible everywhere at once.
    /// </summary>
    public void AdvanceClockTo(DateTime utc) => _timeProvider.Set(utc);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" keeps the local appsettings.Development.json (secrets) out.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", TestJwtSettings.Key);
        builder.UseSetting("Jwt:Issuer", TestJwtSettings.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwtSettings.Audience);
        builder.UseSetting("Jwt:ExpiryMinutes", TestJwtSettings.ExpiryMinutes.ToString());
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");

        // EF Core's verbose SQL logging otherwise interleaves with each
        // test's own captured output; Warning+ keeps that output readable
        // while still surfacing real exceptions (Error) when a test fails.
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(_timeProvider);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
        SeedFixtureUsers(context);

        return host;
    }

    private void SeedFixtureUsers(AppDbContext context)
    {
        // RoleId matches the seeded Roles table: 1 Customer, 2 Staff, 3 Administrator.
        foreach (var (role, roleId) in new[] { ("Customer", 1), ("Staff", 2), ("Administrator", 3) })
        {
            var user = new User
            {
                Email = $"fixture-{role.ToLowerInvariant()}@test.com",
                PasswordHash = "not-used-tokens-are-minted-directly",
                FirstName = "Fixture",
                LastName = role,
                RoleId = roleId,
                IsActive = true,
            };
            context.Users.Add(user);
            _fixtureUserIds[role] = 0; // placeholder; replaced with the real id below
        }

        context.SaveChanges();

        foreach (var user in context.Users.Local)
        {
            if (_fixtureUserIds.ContainsKey(user.LastName))
            {
                _fixtureUserIds[user.LastName] = user.Id;
            }
        }
    }

    /// <summary>Creates a client; pass a role name to send a matching JWT
    /// for that role's real, persisted fixture user.</summary>
    public HttpClient CreateClientAs(string? role)
    {
        var client = CreateClient();

        if (role is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", CreateToken(role));
        }

        return client;
    }

    /// <summary>The database id backing <see cref="CreateClientAs"/> for this role.</summary>
    public int FixtureUserId(string role) => _fixtureUserIds[role];

    private string CreateToken(string role)
    {
        var jwtService = new JwtService(Options.Create(TestJwtSettings));

        if (!_fixtureUserIds.TryGetValue(role, out var userId))
        {
            throw new ArgumentException(
                $"No fixture user is seeded for role '{role}'. Add it to SeedFixtureUsers.", nameof(role));
        }

        return jwtService.GenerateToken(new User
        {
            Id = userId,
            Email = $"fixture-{role.ToLowerInvariant()}@test.com",
            Role = new Role { Name = role }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public MutableTimeProvider(DateTime utcNow)
        {
            _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public void Set(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
