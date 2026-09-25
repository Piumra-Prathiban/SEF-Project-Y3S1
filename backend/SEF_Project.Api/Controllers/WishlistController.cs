using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEF_Project.Api.DTOs.Shopping;
using SEF_Project.Api.Services.Shopping;

namespace SEF_Project.Api.Controllers;

[ApiController]
[Route("api/wishlist")]
[Authorize]
public class WishlistController : ControllerBase
{
    private readonly IWishlistService _wishlistService;

    public WishlistController(IWishlistService wishlistService)
    {
        _wishlistService = wishlistService;
    }

    /// <summary>Gets the authenticated customer's wishlist.</summary>
    /// <response code="200">Returns the wishlist, including an empty wishlist.</response>
    /// <response code="401">The request is not authenticated as an active customer.</response>
    [HttpGet]
    [ProducesResponseType(typeof(WishlistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WishlistResponse>> GetWishlist(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _wishlistService.GetWishlistAsync(
            userId.Value,
            cancellationToken));
    }

    /// <summary>Adds a product to the authenticated customer's wishlist.</summary>
    /// <response code="201">The product was added.</response>
    /// <response code="404">The active product does not exist.</response>
    /// <response code="409">The product is already in the wishlist.</response>
    [HttpPost("items")]
    [ProducesResponseType(typeof(WishlistItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WishlistItemResponse>> AddItem(
        AddWishlistItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _wishlistService.AddItemAsync(
            userId.Value,
            request.ProductId,
            cancellationToken);

        return result.Status switch
        {
            AddWishlistItemStatus.Added => CreatedAtAction(
                nameof(GetWishlist),
                result.Item),
            AddWishlistItemStatus.ProductNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product not found",
                detail: "The requested active product does not exist."),
            AddWishlistItemStatus.Duplicate => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate wishlist item",
                detail: "The product is already in your wishlist."),
            _ => throw new InvalidOperationException(
                "Unexpected wishlist operation result.")
        };
    }

    /// <summary>Removes a product from the authenticated customer's wishlist.</summary>
    /// <response code="204">The product was removed.</response>
    /// <response code="404">The product is not in the customer's wishlist.</response>
    [HttpDelete("items/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var removed = await _wishlistService.RemoveItemAsync(
            userId.Value,
            productId,
            cancellationToken);

        return removed
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Wishlist item not found",
                detail: "The product is not in your wishlist.");
    }

    /// <summary>Gets the number of products in the authenticated customer's wishlist.</summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(WishlistCountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WishlistCountResponse>> GetCount(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var count = await _wishlistService.GetCountAsync(
            userId.Value,
            cancellationToken);

        return Ok(new WishlistCountResponse { Count = count });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out var userId)
            ? userId
            : null;
    }
}
