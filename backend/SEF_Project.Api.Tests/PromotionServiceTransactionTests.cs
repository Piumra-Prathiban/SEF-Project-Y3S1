using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.DTOs.Marketing;
using SEF_Project.Api.Models.Enums;
using SEF_Project.Api.Models.Marketing;
using SEF_Project.Api.Services.Marketing;

namespace SEF_Project.Api.Tests;

/// <summary>
/// Proves <see cref="PromotionService.CreatePromotionsAsync"/> -- the atomic
/// batch-create the Inventory &amp; Promotion Agent uses to turn an approved
/// proposal into real promotions -- really is all-or-nothing: a single
/// invalid item in the batch leaves zero rows behind, not a partial write.
/// </summary>
public class PromotionServiceTransactionTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<(SqliteConnection Connection, AppDbContext Context)> CreateContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();

        return (connection, context);
    }

    private static PromotionRequest ValidRequest(string name, Guid productId) => new()
    {
        Name = name,
        Type = PromotionType.PercentageDiscount,
        DiscountValue = 10m,
        StartDate = Start,
        EndDate = End,
        IsActive = true,
        ProductIds = new List<Guid> { productId },
    };

    [Fact]
    public async Task CreatePromotionsAsync_ShouldPersistAllRequests_WhenEveryRequestIsValid()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var service = new PromotionService(context, TimeProvider.System, NullLogger<PromotionService>.Instance);
        var before = await context.Promotions.CountAsync();

        var created = await service.CreatePromotionsAsync(new[]
        {
            ValidRequest("Batch A", SeedData.ProductMargherita),
            ValidRequest("Batch B", SeedData.ProductCola),
        });

        Assert.Equal(2, created.Count);
        Assert.Equal(before + 2, await context.Promotions.CountAsync());
    }

    [Fact]
    public async Task CreatePromotionsAsync_ShouldPersistNothing_WhenOneRequestInTheBatchIsInvalid()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var service = new PromotionService(context, TimeProvider.System, NullLogger<PromotionService>.Instance);
        var before = await context.Promotions.CountAsync();

        // The first request is perfectly valid on its own; the second names a
        // product that does not exist. Nothing must survive from the first.
        var batch = new[]
        {
            ValidRequest("Would Have Been Valid", SeedData.ProductMargherita),
            ValidRequest("Invalid Product Reference", Guid.NewGuid()),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreatePromotionsAsync(batch));

        Assert.Equal(before, await context.Promotions.CountAsync());
        Assert.False(await context.Promotions.AnyAsync(p => p.Name == "Would Have Been Valid"));
    }

    [Fact]
    public async Task CreatePromotionsAsync_ShouldPersistNothing_WhenABusinessRuleFailsPartway()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var service = new PromotionService(context, TimeProvider.System, NullLogger<PromotionService>.Instance);
        var before = await context.Promotions.CountAsync();

        // A Completed campaign whose own window no longer covers the second
        // request's dates -- ApplyRequestAsync rejects that as
        // InvalidOperationException partway through the batch.
        var closedCampaign = new Campaign
        {
            Name = "Already Finished",
            StartDate = Start,
            EndDate = Start.AddDays(1),
            Status = CampaignStatus.Completed,
        };
        context.Campaigns.Add(closedCampaign);
        await context.SaveChangesAsync();

        var batch = new[]
        {
            ValidRequest("First Item Valid", SeedData.ProductMargherita),
            new PromotionRequest
            {
                Name = "Outside Campaign Dates",
                Type = PromotionType.PercentageDiscount,
                DiscountValue = 10m,
                StartDate = Start,
                EndDate = End,
                IsActive = true,
                CampaignId = closedCampaign.Id,
                ProductIds = new List<Guid> { SeedData.ProductCola },
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePromotionsAsync(batch));

        Assert.Equal(before, await context.Promotions.CountAsync());
    }

    [Fact]
    public async Task CreatePromotionsAsync_ShouldReturnEmptyList_WhenGivenNoRequests()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;
        var service = new PromotionService(context, TimeProvider.System, NullLogger<PromotionService>.Instance);

        var created = await service.CreatePromotionsAsync(Array.Empty<PromotionRequest>());

        Assert.Empty(created);
    }
}
