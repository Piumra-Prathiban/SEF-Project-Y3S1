using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Catalog;
using SEF_Project.Api.Services.Catalog;

namespace SEF_Project.Api.Tests;

public class CategoryServiceTests
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

    [Fact]
    public async Task CreateCategory_ShouldSucceed_WhenNameIsUnique()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var response = await service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "Appetizers",
            Description = "All starter dishes",
            IsActive = true
        });

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("Appetizers", response.Name);
        Assert.Equal("All starter dishes", response.Description);
        Assert.True(response.IsActive);
    }

    [Fact]
    public async Task CreateCategory_ShouldThrow_WhenDuplicateName()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        await service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "Burgers"
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCategoryAsync(new CreateCategoryRequest
            {
                Name = "burgers"
            }));
    }

    [Fact]
    public async Task GetCategories_ShouldFilterInactive_ByDefault()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        await service.CreateCategoryAsync(new CreateCategoryRequest { Name = "Active Cat", IsActive = true });
        await service.CreateCategoryAsync(new CreateCategoryRequest { Name = "Inactive Cat", IsActive = false });

        var activeOnly = await service.GetCategoriesAsync(includeInactive: false);
        var all = await service.GetCategoriesAsync(includeInactive: true);

        Assert.Contains(activeOnly, c => c.Name == "Active Cat");
        Assert.DoesNotContain(activeOnly, c => c.Name == "Inactive Cat");
        Assert.Contains(all, c => c.Name == "Inactive Cat");
    }

    [Fact]
    public async Task UpdateCategory_ShouldModifyFields()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var created = await service.CreateCategoryAsync(new CreateCategoryRequest
        {
            Name = "Pasta Bowls",
            Description = "Old description"
        });

        var updated = await service.UpdateCategoryAsync(created.Id, new UpdateCategoryRequest
        {
            Name = "Italian Pasta Bowls",
            Description = "New description",
            IsActive = true
        });

        Assert.NotNull(updated);
        Assert.Equal("Italian Pasta Bowls", updated.Name);
        Assert.Equal("New description", updated.Description);
    }

    [Fact]
    public async Task DeleteCategory_ShouldRemove_WhenNoProductsAttached()
    {
        var (connection, context) = await CreateContextAsync();
        await using var _ = connection;
        await using var __ = context;

        var service = new CatalogService(context);

        var created = await service.CreateCategoryAsync(new CreateCategoryRequest { Name = "Seasonal Special Desserts" });

        var deleted = await service.DeleteCategoryAsync(created.Id);
        Assert.True(deleted);

        var fetched = await service.GetCategoryByIdAsync(created.Id);
        Assert.Null(fetched);
    }
}
