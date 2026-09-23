using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.Data.Configurations;
using SEF_Project.Api.Models;
using SEF_Project.Api.Models.Shopping;

namespace SEF_Project.Api.Tests;

public class ShoppingDatabaseIntegrationTests
{
    [Fact]
    public async Task Wishlist_ShouldRejectDuplicateProduct()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "wishlist@example.com");
        var wishlist = new Wishlist { CustomerId = customer.Id };

        context.Wishlists.Add(wishlist);
        wishlist.Items.Add(new WishlistItem
        {
            ProductId = SeedData.ProductMargherita
        });
        await context.SaveChangesAsync();

        context.WishlistItems.Add(new WishlistItem
        {
            WishlistId = wishlist.Id,
            ProductId = SeedData.ProductMargherita
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CartItem_ShouldRejectNonPositiveQuantity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "cart-quantity@example.com");
        var cart = new Cart { CustomerId = customer.Id };

        cart.Items.Add(new CartItem
        {
            ProductVariantId = SeedData.VariantMargheritaSmall,
            Quantity = 0
        });
        context.Carts.Add(cart);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Customer_ShouldNotOwnMultipleCartsOrWishlists()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "ownership@example.com");

        context.Carts.Add(new Cart { CustomerId = customer.Id });
        context.Wishlists.Add(new Wishlist { CustomerId = customer.Id });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();
        context.Carts.Add(new Cart { CustomerId = customer.Id });
        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        context.Wishlists.Add(new Wishlist { CustomerId = customer.Id });
        await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task ShoppingEntities_ShouldReceiveAuditTimestamps()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "audit@example.com");
        var cart = new Cart { CustomerId = customer.Id };
        var wishlist = new Wishlist { CustomerId = customer.Id };

        context.AddRange(cart, wishlist);
        await context.SaveChangesAsync();

        Assert.NotEqual(default, cart.CreatedAt);
        Assert.Equal(cart.CreatedAt, cart.UpdatedAt);
        Assert.NotEqual(default, wishlist.CreatedAt);
        Assert.Equal(wishlist.CreatedAt, wishlist.UpdatedAt);
    }

    private static async Task<AppDbContext> CreateContextAsync(
        SqliteConnection connection)
    {
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return context;
    }

    private static async Task<Customer> AddCustomerAsync(
        AppDbContext context,
        string email)
    {
        var customer = new Customer
        {
            User = new User
            {
                Email = email,
                PasswordHash = "test-only-password-hash",
                FirstName = "Test",
                LastName = "Customer",
                RoleId = 1
            }
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        return customer;
    }
}
