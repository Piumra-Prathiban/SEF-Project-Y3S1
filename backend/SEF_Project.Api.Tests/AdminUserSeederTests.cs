using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Configuration;
using SEF_Project.Api.Data;
using SEF_Project.Api.Models;
using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Tests;

public class AdminUserSeederTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Context)>
        CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    private static AdminSeedSettings Settings(
        string? email = "admin@clothic.local",
        string? password = "Admin#12345") =>
        new()
        {
            Email = email,
            Password = password,
            FirstName = "Clothic",
            LastName = "Admin"
        };

    private static Task SeedAsync(
        AppDbContext context,
        AdminSeedSettings settings) =>
        AdminUserSeeder.SeedAsync(
            context,
            new PasswordService(),
            settings,
            NullLogger.Instance);

    [Fact]
    public async Task SeedAsync_ShouldCreateAdministratorWithHashedPassword()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        await SeedAsync(context, Settings());

        var admin = await context.Users
            .Include(u => u.Role)
            .SingleAsync();

        Assert.Equal("admin@clothic.local", admin.Email);
        Assert.Equal("Administrator", admin.Role.Name);
        Assert.Equal("Clothic", admin.FirstName);
        Assert.Equal("Admin", admin.LastName);
        Assert.True(admin.IsActive);
        Assert.NotEqual("Admin#12345", admin.PasswordHash);
        Assert.True(
            new PasswordService().VerifyPassword("Admin#12345", admin.PasswordHash));
    }

    [Fact]
    public async Task SeedAsync_ShouldNormaliseTheEmailCase()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        await SeedAsync(context, Settings(email: "  Admin@Clothic.Local  "));

        var admin = await context.Users.SingleAsync();

        Assert.Equal("admin@clothic.local", admin.Email);
    }

    [Fact]
    public async Task SeedAsync_ShouldSkipWhenNotConfigured()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        await SeedAsync(context, Settings(email: null, password: null));
        await SeedAsync(context, Settings(email: "admin@clothic.local", password: "  "));

        Assert.Empty(await context.Users.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_ShouldLeaveAnExistingAccountUntouched()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var customerRole = await context.Roles
            .SingleAsync(r => r.Name == "Customer");

        context.Users.Add(new User
        {
            Email = "admin@clothic.local",
            PasswordHash = "existing-hash",
            FirstName = "Existing",
            LastName = "Person",
            IsActive = true,
            RoleId = customerRole.Id
        });

        await context.SaveChangesAsync();

        await SeedAsync(context, Settings());

        var user = await context.Users
            .Include(u => u.Role)
            .SingleAsync();

        // No silent password reset and no privilege escalation.
        Assert.Equal("existing-hash", user.PasswordHash);
        Assert.Equal("Customer", user.Role.Name);
        Assert.Equal("Existing", user.FirstName);
    }

    [Fact]
    public async Task SeedAsync_ShouldSkipWhenTheAdministratorRoleIsMissing()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var role = await context.Roles
            .SingleAsync(r => r.Name == "Administrator");

        context.Roles.Remove(role);
        await context.SaveChangesAsync();

        await SeedAsync(context, Settings());

        Assert.Empty(await context.Users.ToListAsync());
    }
}
