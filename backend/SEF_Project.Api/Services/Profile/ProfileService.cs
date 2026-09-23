using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Profile;
using SEF_Project.Api.Models;

namespace SEF_Project.Api.Services.Profile;

public class ProfileService : IProfileService
{
    private readonly AppDbContext _context;

    public ProfileService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ProfileResponse> GetProfileAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var customer = await LoadCustomerAsync(
            userId,
            asNoTracking: true,
            cancellationToken);

        return MapProfile(customer);
    }

    public async Task<UpdateProfileResult> UpdateProfileAsync(
        int userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var customer = await LoadCustomerAsync(
            userId,
            asNoTracking: false,
            cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(
                user => user.Id != userId && user.Email == email,
                cancellationToken))
        {
            return new UpdateProfileResult(UpdateProfileStatus.EmailInUse);
        }

        customer.User.Email = email;
        customer.User.FirstName = request.FirstName.Trim();
        customer.User.LastName = request.LastName.Trim();
        customer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateProfileResult(
            UpdateProfileStatus.Updated,
            MapProfile(customer));
    }

    public async Task<IReadOnlyList<AddressResponse>> GetAddressesAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveCustomerIdAsync(userId, cancellationToken);

        return await _context.Addresses
            .AsNoTracking()
            .Where(address => address.CustomerId == customerId)
            .OrderByDescending(address => address.IsDefault)
            .ThenBy(address => address.Label)
            .ThenBy(address => address.Id)
            .Select(address => MapAddress(address))
            .ToListAsync(cancellationToken);
    }

    public async Task<AddressResponse> CreateAddressAsync(
        int userId,
        CreateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var customerId = await ResolveCustomerIdAsync(userId, cancellationToken);
        var existingAddresses = await _context.Addresses
            .Where(address => address.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        var makeDefault = request.IsDefault || existingAddresses.Count == 0;

        if (makeDefault)
        {
            UnsetDefaults(existingAddresses);
        }

        var address = new Address
        {
            CustomerId = customerId,
            IsDefault = makeDefault
        };

        ApplyAddress(address, request);
        address.IsDefault = makeDefault;
        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        return MapAddress(address);
    }

    public async Task<AddressResponse?> UpdateAddressAsync(
        int userId,
        int addressId,
        UpdateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var customerId = await ResolveCustomerIdAsync(userId, cancellationToken);
        var address = await _context.Addresses.SingleOrDefaultAsync(
            item => item.Id == addressId && item.CustomerId == customerId,
            cancellationToken);

        if (address == null)
        {
            return null;
        }

        if (request.IsDefault)
        {
            var otherDefaults = await _context.Addresses
                .Where(item =>
                    item.CustomerId == customerId &&
                    item.Id != addressId &&
                    item.IsDefault)
                .ToListAsync(cancellationToken);

            UnsetDefaults(otherDefaults);
        }

        ApplyAddress(address, request);
        await _context.SaveChangesAsync(cancellationToken);
        return MapAddress(address);
    }

    public async Task<bool> DeleteAddressAsync(
        int userId,
        int addressId,
        CancellationToken cancellationToken = default)
    {
        var customerId = await ResolveCustomerIdAsync(userId, cancellationToken);
        var address = await _context.Addresses.SingleOrDefaultAsync(
            item => item.Id == addressId && item.CustomerId == customerId,
            cancellationToken);

        if (address == null)
        {
            return false;
        }

        if (address.IsDefault)
        {
            var replacement = await _context.Addresses
                .Where(item =>
                    item.CustomerId == customerId && item.Id != addressId)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (replacement != null)
            {
                replacement.IsDefault = true;
            }
        }

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Customer> LoadCustomerAsync(
        int userId,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Customer> query = _context.Customers
            .Include(customer => customer.User)
            .Where(customer =>
                customer.UserId == userId && customer.User.IsActive);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "An active customer profile is required.");
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

    private static void ValidateRequest(object request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                request,
                new ValidationContext(request),
                validationResults,
                validateAllProperties: true))
        {
            throw new ArgumentException(
                validationResults[0].ErrorMessage ?? "The request is invalid.");
        }

        var requiredText = request switch
        {
            UpdateProfileRequest profile => new[]
            {
                profile.Email,
                profile.FirstName,
                profile.LastName
            },
            AddressRequest address => new[]
            {
                address.Label,
                address.AddressLine1,
                address.City,
                address.PostalCode,
                address.Country
            },
            _ => Array.Empty<string>()
        };

        if (requiredText.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Required fields cannot contain only whitespace.");
        }
    }

    private static void ApplyAddress(Address address, AddressRequest request)
    {
        address.Label = request.Label.Trim();
        address.AddressLine1 = request.AddressLine1.Trim();
        address.AddressLine2 = NormalizeOptional(request.AddressLine2);
        address.City = request.City.Trim();
        address.Province = NormalizeOptional(request.Province);
        address.PostalCode = request.PostalCode.Trim();
        address.Country = request.Country.Trim();
        address.IsDefault = request.IsDefault;
    }

    private static void UnsetDefaults(IEnumerable<Address> addresses)
    {
        foreach (var address in addresses.Where(item => item.IsDefault))
        {
            address.IsDefault = false;
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProfileResponse MapProfile(Customer customer) => new()
    {
        CustomerId = customer.Id,
        Email = customer.User.Email,
        FirstName = customer.User.FirstName,
        LastName = customer.User.LastName,
        MemberSince = customer.CreatedAt
    };

    private static AddressResponse MapAddress(Address address) => new()
    {
        Id = address.Id,
        Label = address.Label,
        AddressLine1 = address.AddressLine1,
        AddressLine2 = address.AddressLine2,
        City = address.City,
        Province = address.Province,
        PostalCode = address.PostalCode,
        Country = address.Country,
        IsDefault = address.IsDefault
    };
}
