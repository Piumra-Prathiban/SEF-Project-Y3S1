using SEF_Project.Api.DTOs.Shopping;

namespace SEF_Project.Api.Services.Shopping;

public interface IProductSearchService
{
    Task<PagedProductResponse> SearchAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default);
}
