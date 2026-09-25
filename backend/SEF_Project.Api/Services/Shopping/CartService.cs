using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Models.Catalog;
using SEF_Project.Api.Models.Shopping;

namespace SEF_Project.Api.Services.Shopping;

public class CartService : ICartService
{
    private readonly AppDbContext _context;

    public CartService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CartResponse> GetCartAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCustomerAsync(userId, cancellationToken);

        var cart = await LoadOwnedCartAsync(
            userId,
            asNoTracking: true,
            cancellationToken);

        return cart == null ? new CartResponse() : MapCart(cart);
    }

    public async Task<CartMutationResult> AddItemAsync(
        int userId,
        AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateQuantity(request.Quantity);

        if (request.ProductVariantId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid product variant identifier is required.");
        }

        var customerId = await ResolveCustomerIdAsync(userId, cancellationToken);
        var variant = await LoadVariantAsync(
            request.ProductVariantId,
            cancellationToken);

        var validationStatus = ValidateVariant(variant);

        if (validationStatus.HasValue)
        {
            return new CartMutationResult(validationStatus.Value);
        }

        var cart = await _context.Carts
            .Include(item => item.Items)
            .SingleOrDefaultAsync(
                item => item.CustomerId == customerId,
                cancellationToken);

        var isNewCart = cart == null;
        cart ??= new Cart { CustomerId = customerId };

        var existingItem = cart.Items.SingleOrDefault(item =>
            item.ProductVariantId == request.ProductVariantId);
        var requestedQuantity =
            (long)(existingItem?.Quantity ?? 0) + request.Quantity;

        if (requestedQuantity > int.MaxValue ||
            !HasSufficientStock(variant!, (int)requestedQuantity))
        {
            return new CartMutationResult(
                CartMutationStatus.InsufficientStock);
        }

        CartMutationStatus status;

        if (existingItem == null)
        {
            if (isNewCart)
            {
                _context.Carts.Add(cart);
            }

            cart.Items.Add(new CartItem
            {
                ProductVariantId = request.ProductVariantId,
                Quantity = request.Quantity
            });
            status = CartMutationStatus.Added;
        }
        else
        {
            existingItem.Quantity = (int)requestedQuantity;
            status = CartMutationStatus.Updated;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new CartMutationResult(
            status,
            await GetCartAsync(userId, cancellationToken));
    }

    public async Task<CartMutationResult> UpdateItemAsync(
        int userId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        ValidateQuantity(quantity);

        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("A valid cart item identifier is required.");
        }

        await EnsureCustomerAsync(userId, cancellationToken);

        var item = await _context.CartItems
            .Include(cartItem => cartItem.ProductVariant)
                .ThenInclude(variant => variant.Product)
            .Include(cartItem => cartItem.ProductVariant)
                .ThenInclude(variant => variant.InventoryStock)
            .SingleOrDefaultAsync(
                cartItem => cartItem.Id == itemId &&
                    cartItem.Cart.Customer.UserId == userId,
                cancellationToken);

        if (item == null)
        {
            return new CartMutationResult(CartMutationStatus.ItemNotFound);
        }

        var validationStatus = ValidateVariant(item.ProductVariant);

        if (validationStatus.HasValue)
        {
            return new CartMutationResult(validationStatus.Value);
        }

        if (!HasSufficientStock(item.ProductVariant, quantity))
        {
            return new CartMutationResult(
                CartMutationStatus.InsufficientStock);
        }

        item.Quantity = quantity;
        await _context.SaveChangesAsync(cancellationToken);

        return new CartMutationResult(
            CartMutationStatus.Updated,
            await GetCartAsync(userId, cancellationToken));
    }

    public async Task<bool> RemoveItemAsync(
        int userId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("A valid cart item identifier is required.");
        }

        await EnsureCustomerAsync(userId, cancellationToken);

        var item = await _context.CartItems.SingleOrDefaultAsync(
            cartItem => cartItem.Id == itemId &&
                cartItem.Cart.Customer.UserId == userId,
            cancellationToken);

        if (item == null)
        {
            return false;
        }

        _context.CartItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ClearCartAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCustomerAsync(userId, cancellationToken);

        var items = await _context.CartItems
            .Where(item => item.Cart.Customer.UserId == userId)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return;
        }

        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Cart?> LoadOwnedCartAsync(
        int userId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Cart> query = _context.Carts
            .AsSplitQuery()
            .Include(item => item.Items)
                .ThenInclude(item => item.ProductVariant)
                    .ThenInclude(variant => variant.Product)
            .Include(item => item.Items)
                .ThenInclude(item => item.ProductVariant)
                    .ThenInclude(variant => variant.InventoryStock)
            .Where(item => item.Customer.UserId == userId);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ProductVariant?> LoadVariantAsync(
        Guid variantId,
        CancellationToken cancellationToken) =>
        await _context.ProductVariants
            .Include(variant => variant.Product)
            .Include(variant => variant.InventoryStock)
            .SingleOrDefaultAsync(
                variant => variant.Id == variantId,
                cancellationToken);

    private static CartMutationStatus? ValidateVariant(ProductVariant? variant)
    {
        if (variant == null)
        {
            return CartMutationStatus.VariantNotFound;
        }

        if (!variant.IsActive || !variant.Product.IsActive)
        {
            return CartMutationStatus.VariantUnavailable;
        }

        return null;
    }

    private static bool HasSufficientStock(
        ProductVariant variant,
        int quantity) =>
        variant.InventoryStock != null &&
        variant.InventoryStock.QuantityOnHand -
            variant.InventoryStock.ReservedQuantity >= quantity;

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.");
        }
    }

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

    private static CartResponse MapCart(Cart cart)
    {
        var items = cart.Items
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(MapItem)
            .ToList();
        var subtotal = items.Sum(item => item.LineTotal);

        return new CartResponse
        {
            Id = cart.Id,
            Items = items,
            TotalQuantity = items.Sum(item => item.Quantity),
            Subtotal = subtotal,
            Total = subtotal
        };
    }

    private static CartItemResponse MapItem(CartItem item)
    {
        var variant = item.ProductVariant;
        var availableQuantity = variant.InventoryStock == null
            ? 0
            : Math.Max(
                0,
                variant.InventoryStock.QuantityOnHand -
                    variant.InventoryStock.ReservedQuantity);
        var lineTotal = Math.Round(variant.Price * item.Quantity, 2);
        var isAvailable = variant.IsActive &&
            variant.Product.IsActive &&
            availableQuantity > 0;

        return new CartItemResponse
        {
            Id = item.Id,
            ProductVariantId = variant.Id,
            ProductId = variant.ProductId,
            ProductName = variant.Product.Name,
            VariantName = variant.Name,
            Sku = variant.Sku,
            UnitPrice = variant.Price,
            Quantity = item.Quantity,
            LineTotal = lineTotal,
            AvailableQuantity = availableQuantity,
            IsAvailable = isAvailable,
            HasSufficientStock = isAvailable &&
                availableQuantity >= item.Quantity
        };
    }
}
