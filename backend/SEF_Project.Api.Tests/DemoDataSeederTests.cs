using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Tests;

public class DemoDataSeederTests
{
    private static readonly DateTime SeedTime =
        new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(SeedTime, TimeSpan.Zero);
    }

    [Fact]
    public async Task SeedAndReset_CreateAnIdempotentConnectedDatasetAndPreserveMigrationData()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var context = new AppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var passwords = new PasswordService();
            var seeder = new DemoDataSeeder(context, passwords, new FixedTimeProvider());

            var first = await seeder.SeedAsync(new DemoDataOptions
            {
                Password = "DemoPassword123!"
            });
            var second = await seeder.SeedAsync(new DemoDataOptions
            {
                Password = "DemoPassword123!"
            });

            Assert.True(first.Changed);
            Assert.False(second.Changed);
            Assert.Equal(3, await context.Users.CountAsync(user =>
                user.Email.EndsWith("@demo.clothic")));
            Assert.True(passwords.VerifyPassword(
                "DemoPassword123!",
                await context.Users
                    .Where(user => user.Email == DemoDataSeeder.CustomerEmail)
                    .Select(user => user.PasswordHash)
                    .SingleAsync()));
            Assert.Equal(4, await context.Orders.CountAsync(order =>
                order.OrderNumber.StartsWith("DEMO-ORD-")));
            Assert.Contains(
                await context.Orders
                    .Where(order => order.OrderNumber.StartsWith("DEMO-ORD-"))
                    .Select(order => order.Status)
                    .ToListAsync(),
                status => status == OrderStatus.Completed);
            Assert.Single(await context.Returns.ToListAsync());
            Assert.Single(await context.CouponRedemptions.ToListAsync());
            Assert.Equal(2, await context.AgentWorkflows.CountAsync());
            var campaign = await context.Campaigns
                .SingleAsync(item => item.Name == "Demo Seasonal Edit");
            Assert.Equal(SeedTime.Date.AddDays(-7), campaign.StartDate);
            Assert.Equal(SeedTime.Date.AddDays(30), campaign.EndDate);
        }

        await using (var resetContext = new AppDbContext(options))
        {
            var seeder = new DemoDataSeeder(
                resetContext,
                new PasswordService(),
                new FixedTimeProvider());

            var firstReset = await seeder.ResetAsync();
            var secondReset = await seeder.ResetAsync();

            Assert.True(firstReset.Changed);
            Assert.False(secondReset.Changed);
            Assert.Empty(await resetContext.Users
                .Where(user => user.Email.EndsWith("@demo.clothic"))
                .ToListAsync());
            Assert.Empty(await resetContext.Orders
                .Where(order => order.OrderNumber.StartsWith("DEMO-ORD-"))
                .ToListAsync());
            Assert.True(await resetContext.Products.AnyAsync(product =>
                product.Id == SeedData.ProductTShirt));
            Assert.True(await resetContext.Roles.AnyAsync(role =>
                role.Name == "Administrator"));
        }
    }

    [Fact]
    public async Task SeedAsync_RequiresAnExplicitDemoPassword()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options);
        await context.Database.EnsureCreatedAsync();
        var seeder = new DemoDataSeeder(
            context,
            new PasswordService(),
            new FixedTimeProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            seeder.SeedAsync(new DemoDataOptions()));

        Assert.Contains("DemoData__Password", exception.Message);
        Assert.Empty(await context.Users
            .Where(user => user.Email.EndsWith("@demo.clothic"))
            .ToListAsync());
    }
}
