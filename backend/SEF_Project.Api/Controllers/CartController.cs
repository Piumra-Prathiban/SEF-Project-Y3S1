using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    /// <summary>Gets the authenticated customer's cart.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CartResponse>> GetCart(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _cartService.GetCartAsync(
            userId.Value,
            cancellationToken));
    }

    /// <summary>Adds a variant or increases its existing cart quantity.</summary>
    /// <remarks>
    /// Price, product details, and available stock are always read from the
    /// database. Adding to a cart does not reserve inventory; checkout performs
    /// the final stock validation and reservation.
    /// </remarks>
    [HttpPost("items")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartResponse>> AddItem(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _cartService.AddItemAsync(
            userId.Value,
            request,
            cancellationToken);

        return result.Status == CartMutationStatus.Added
            ? CreatedAtAction(nameof(GetCart), result.Cart)
            : ToActionResult(result);
    }

    /// <summary>Replaces the quantity of an owned cart item.</summary>
    [HttpPut("items/{id:guid}")]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartResponse>> UpdateItem(
        Guid id,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return ToActionResult(await _cartService.UpdateItemAsync(
            userId.Value,
            id,
            request.Quantity,
            cancellationToken));
    }

    /// <summary>Removes an owned cart item.</summary>
    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return await _cartService.RemoveItemAsync(
            userId.Value,
            id,
            cancellationToken)
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Cart item not found",
                detail: "The cart item does not exist in your cart.");
    }

    /// <summary>Removes all items from the authenticated customer's cart.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClearCart(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        await _cartService.ClearCartAsync(userId.Value, cancellationToken);
        return NoContent();
    }

    private ActionResult<CartResponse> ToActionResult(CartMutationResult result) =>
        result.Status switch
        {
            CartMutationStatus.Updated => Ok(result.Cart),
            CartMutationStatus.ItemNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Cart item not found",
                detail: "The cart item does not exist in your cart."),
            CartMutationStatus.VariantNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product variant not found",
                detail: "The requested product variant does not exist."),
            CartMutationStatus.VariantUnavailable => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Product variant unavailable",
                detail: "The requested product variant is not available."),
            CartMutationStatus.InsufficientStock => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Insufficient stock",
                detail: "The requested quantity exceeds available stock."),
            _ => throw new InvalidOperationException(
                "Unexpected cart operation result.")
        };

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
