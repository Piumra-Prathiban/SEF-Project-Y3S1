namespace SEF_Project.Api.Services.Recommendations;

public interface ICustomerPreferenceTool
{
    Task<CustomerPreferenceToolOutput> ExecuteAsync(
        CustomerPreferenceToolInput input,
        CancellationToken cancellationToken = default);
}

public interface IProductSearchTool
{
    Task<ProductSearchToolOutput> ExecuteAsync(
        ProductSearchToolInput input,
        CancellationToken cancellationToken = default);
}

public interface IWishlistTool
{
    Task<WishlistToolOutput> ExecuteAsync(
        WishlistToolInput input,
        CancellationToken cancellationToken = default);
}

public interface IProductAvailabilityTool
{
    Task<ProductAvailabilityToolOutput> ExecuteAsync(
        ProductAvailabilityToolInput input,
        CancellationToken cancellationToken = default);
}
