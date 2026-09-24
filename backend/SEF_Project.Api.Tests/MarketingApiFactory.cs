using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    public MarketingApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" keeps the local appsettings.Development.json (secrets) out.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", TestJwtSettings.Key);
        builder.UseSetting("Jwt:Issuer", TestJwtSettings.Issuer);
        builder.UseSetting("Jwt:Audience", TestJwtSettings.Audience);
        builder.UseSetting("Jwt:ExpiryMinutes", TestJwtSettings.ExpiryMinutes.ToString());
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.EnsureCreated();

        return host;
    }

    /// <summary>Creates a client; pass a role name to send a matching JWT.</summary>
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

    private static string CreateToken(string role)
    {
        var jwtService = new JwtService(Options.Create(TestJwtSettings));

        return jwtService.GenerateToken(new User
        {
            Id = 1000,
            Email = $"{role.ToLowerInvariant()}@test.com",
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime utcNow)
        {
            _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
