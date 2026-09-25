using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Shopping;

namespace SEF_Project.Api.Services.Shopping;

public class WishlistService : IWishlistService
{
    private readonly AppDbContext _context;

    public WishlistService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WishlistResponse> GetWishlistAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCustomerAsync(userId, cancellationToken);

        var wishlist = await OwnedWishlists(userId)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Variants)
                        .ThenInclude(variant => variant.InventoryStock)
            .SingleOrDefaultAsync(cancellationToken);

        return wishlist == null
            ? new WishlistResponse()
            : MapWishlist(wishlist);
    }

    public async Task<AddWishlistItemResult> AddItemAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A valid product identifier is required.");
        }

        var customerId = await ResolveCustomerIdAsync(userId, cancellationToken);

        var product = await _context.Products
            .Include(item => item.Variants)
                .ThenInclude(variant => variant.InventoryStock)
            .SingleOrDefaultAsync(
                item => item.Id == productId && item.IsActive,
                cancellationToken);

        if (product == null)
        {
            return new AddWishlistItemResult(
                AddWishlistItemStatus.ProductNotFound);
        }

        var wishlist = await _context.Wishlists
            .SingleOrDefaultAsync(
                item => item.CustomerId == customerId,
                cancellationToken);

        if (wishlist != null && await _context.WishlistItems.AnyAsync(
                item => item.WishlistId == wishlist.Id &&
                    item.ProductId == productId,
                cancellationToken))
        {
            return new AddWishlistItemResult(AddWishlistItemStatus.Duplicate);
        }

        wishlist ??= new Wishlist { CustomerId = customerId };

        if (wishlist.Id == Guid.Empty)
        {
            _context.Wishlists.Add(wishlist);
        }

        var wishlistItem = new WishlistItem
        {
            ProductId = productId,
            Product = product
        };

        wishlist.Items.Add(wishlistItem);
        await _context.SaveChangesAsync(cancellationToken);

        return new AddWishlistItemResult(
            AddWishlistItemStatus.Added,
            MapItem(wishlistItem));
    }

    public async Task<bool> RemoveItemAsync(
        int userId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("A valid product identifier is required.");
        }

        await EnsureCustomerAsync(userId, cancellationToken);

        var item = await _context.WishlistItems
            .SingleOrDefaultAsync(
                wishlistItem =>
                    wishlistItem.ProductId == productId &&
                    wishlistItem.Wishlist.Customer.UserId == userId,
                cancellationToken);

        if (item == null)
        {
            return false;
        }

        _context.WishlistItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GetCountAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCustomerAsync(userId, cancellationToken);

        return await _context.WishlistItems.CountAsync(
            item => item.Wishlist.Customer.UserId == userId,
            cancellationToken);
    }

    private IQueryable<Wishlist> OwnedWishlists(int userId) =>
        _context.Wishlists.Where(item => item.Customer.UserId == userId);

    private async Task EnsureCustomerAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        _ = await ResolveCustomerIdAsync(userId, cancellationToken);
    }

    private async Task<int> ResolveCustomerIdAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var customerId = await _context.Customers
            .Where(customer =>
                customer.UserId == userId && customer.User.IsActive)
            .Select(customer => (int?)customer.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return customerId ?? throw new UnauthorizedAccessException(
            "An active customer profile is required.");
    }

    private static WishlistResponse MapWishlist(Wishlist wishlist) => new()
    {
        Id = wishlist.Id,
        Items = wishlist.Items
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.ProductId)
            .Select(MapItem)
            .ToList()
    };

    private static WishlistItemResponse MapItem(WishlistItem item)
    {
        var activeVariants = item.Product.Variants
            .Where(variant => variant.IsActive)
            .ToList();

        return new WishlistItemResponse
        {
            ProductId = item.ProductId,
            ProductName = item.Product.Name,
            Description = item.Product.Description,
            MinimumPrice = activeVariants.Count == 0
                ? null
                : activeVariants.Min(variant => variant.Price),
            IsProductActive = item.Product.IsActive,
            IsAvailable = item.Product.IsActive && activeVariants.Any(variant =>
                variant.InventoryStock != null &&
                variant.InventoryStock.QuantityOnHand -
                    variant.InventoryStock.ReservedQuantity > 0),
            AddedAt = item.CreatedAt
        };
    }
}
