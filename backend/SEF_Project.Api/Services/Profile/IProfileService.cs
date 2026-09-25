using SEF_Project.Api.DTOs.Profile;

namespace SEF_Project.Api.Services.Profile;

public enum UpdateProfileStatus
{
    Updated,
    EmailInUse
}

public record UpdateProfileResult(
    UpdateProfileStatus Status,
    ProfileResponse? Profile = null);

public interface IProfileService
{
    Task<ProfileResponse> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<UpdateProfileResult> UpdateProfileAsync(
        int userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AddressResponse>> GetAddressesAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<AddressResponse> CreateAddressAsync(
        int userId,
        CreateAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<AddressResponse?> UpdateAddressAsync(
        int userId,
        int addressId,
        UpdateAddressRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAddressAsync(
        int userId,
        int addressId,
        CancellationToken cancellationToken = default);
}
