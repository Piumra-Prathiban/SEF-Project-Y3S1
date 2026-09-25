using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Controllers;
using SEF_Project.Api.Data;
using SEF_Project.Api.DTOs.Profile;
using SEF_Project.Api.Models;
using SEF_Project.Api.Services.Profile;

namespace SEF_Project.Api.Tests;

public class ProfileServiceTests
{
    [Fact]
    public void ProfileController_RequiresAuthentication()
    {
        Assert.NotNull(typeof(ProfileController)
            .GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task GetProfile_ReturnsUnauthorizedWithoutUserClaim()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var controller = CreateController(new ProfileService(context));

        var result = await controller.GetProfile(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetProfile_ReturnsAuthenticatedCustomersExistingUserData()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(
            context,
            "profile@test.com",
            "Nimal",
            "Perera");

        var profile = await new ProfileService(context)
            .GetProfileAsync(customer.UserId);

        Assert.Equal(customer.CustomerId, profile.CustomerId);
        Assert.Equal("profile@test.com", profile.Email);
        Assert.Equal("Nimal", profile.FirstName);
        Assert.Equal("Perera", profile.LastName);
        Assert.NotEqual(default, profile.MemberSince);
        Assert.DoesNotContain(
            typeof(ProfileResponse).GetProperties(),
            property => property.Name.Contains(
                "Password",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateProfile_UpdatesAndNormalizesExistingUserData()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "old@test.com");
        var service = new ProfileService(context);

        var result = await service.UpdateProfileAsync(
            customer.UserId,
            new UpdateProfileRequest
            {
                Email = "  NEW@Example.COM ",
                FirstName = "  Amal ",
                LastName = " Silva  "
            });

        Assert.Equal(UpdateProfileStatus.Updated, result.Status);
        Assert.Equal("new@example.com", result.Profile!.Email);
        Assert.Equal("Amal", result.Profile.FirstName);
        Assert.Equal("Silva", result.Profile.LastName);

        var user = await context.Users.SingleAsync(item =>
            item.Id == customer.UserId);
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal("Amal", user.FirstName);
    }

    [Fact]
    public async Task UpdateProfile_RejectsEmailOwnedByAnotherUser()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customerA = await AddCustomerAsync(context, "profile-a@test.com");
        _ = await AddCustomerAsync(context, "profile-b@test.com");

        var result = await new ProfileService(context).UpdateProfileAsync(
            customerA.UserId,
            new UpdateProfileRequest
            {
                Email = "PROFILE-B@test.com",
                FirstName = "Customer",
                LastName = "A"
            });

        Assert.Equal(UpdateProfileStatus.EmailInUse, result.Status);
        Assert.Equal(
            "profile-a@test.com",
            (await context.Users.FindAsync(customerA.UserId))!.Email);
    }

    [Fact]
    public async Task CreateAddress_CreatesTrimmedFirstDefaultAddress()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "create-address@test.com");

        var address = await new ProfileService(context).CreateAddressAsync(
            customer.UserId,
            CreateAddress("  Home  ", isDefault: false));

        Assert.True(address.IsDefault);
        Assert.Equal("Home", address.Label);
        Assert.Equal("10 Main Street", address.AddressLine1);
        Assert.Null(address.AddressLine2);
        Assert.Equal(
            customer.CustomerId,
            (await context.Addresses.FindAsync(address.Id))!.CustomerId);
    }

    [Fact]
    public async Task UpdateAddress_UpdatesOwnedAddress()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "update-address@test.com");
        var service = new ProfileService(context);
        var created = await service.CreateAddressAsync(
            customer.UserId,
            CreateAddress("Home"));

        var updated = await service.UpdateAddressAsync(
            customer.UserId,
            created.Id,
            UpdateAddress("Office", isDefault: true));

        Assert.NotNull(updated);
        Assert.Equal("Office", updated.Label);
        Assert.Equal("20 Updated Road", updated.AddressLine1);
        Assert.Equal("Colombo", updated.City);
        Assert.True(updated.IsDefault);
    }

    [Fact]
    public async Task DeleteAddress_DeletesOwnedAddress()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "delete-address@test.com");
        var service = new ProfileService(context);
        var created = await service.CreateAddressAsync(
            customer.UserId,
            CreateAddress("Temporary"));

        var deleted = await service.DeleteAddressAsync(
            customer.UserId,
            created.Id);

        Assert.True(deleted);
        Assert.Null(await context.Addresses.FindAsync(created.Id));
    }

    [Fact]
    public async Task DefaultAddress_ChangesAndPromotesReplacementOnDeletion()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "defaults@test.com");
        var service = new ProfileService(context);
        var home = await service.CreateAddressAsync(
            customer.UserId,
            CreateAddress("Home"));
        var office = await service.CreateAddressAsync(
            customer.UserId,
            CreateAddress("Office", isDefault: true));

        var afterOffice = await service.GetAddressesAsync(customer.UserId);
        Assert.False(afterOffice.Single(item => item.Id == home.Id).IsDefault);
        Assert.True(afterOffice.Single(item => item.Id == office.Id).IsDefault);
        Assert.Single(afterOffice.Where(item => item.IsDefault));

        _ = await service.UpdateAddressAsync(
            customer.UserId,
            home.Id,
            UpdateAddress("Home", isDefault: true));

        var afterHome = await service.GetAddressesAsync(customer.UserId);
        Assert.True(afterHome.Single(item => item.Id == home.Id).IsDefault);
        Assert.False(afterHome.Single(item => item.Id == office.Id).IsDefault);
        Assert.Single(afterHome.Where(item => item.IsDefault));

        Assert.True(await service.DeleteAddressAsync(customer.UserId, home.Id));

        var afterDeletion = await service.GetAddressesAsync(customer.UserId);
        Assert.True(Assert.Single(afterDeletion).IsDefault);
        Assert.Equal(office.Id, afterDeletion[0].Id);
    }

    [Fact]
    public async Task CustomerCannotAccessUpdateOrDeleteAnotherCustomersAddress()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customerA = await AddCustomerAsync(context, "address-a@test.com");
        var customerB = await AddCustomerAsync(context, "address-b@test.com");
        var service = new ProfileService(context);
        var addressB = await service.CreateAddressAsync(
            customerB.UserId,
            CreateAddress("Private"));

        var addressesA = await service.GetAddressesAsync(customerA.UserId);
        var update = await service.UpdateAddressAsync(
            customerA.UserId,
            addressB.Id,
            UpdateAddress("Changed"));
        var deleted = await service.DeleteAddressAsync(
            customerA.UserId,
            addressB.Id);

        Assert.Empty(addressesA);
        Assert.Null(update);
        Assert.False(deleted);
        Assert.NotNull(await context.Addresses.FindAsync(addressB.Id));
    }

    [Fact]
    public async Task ProfileAndAddressRequests_RejectInvalidData()
    {
        var invalidProfile = new UpdateProfileRequest
        {
            Email = "not-an-email",
            FirstName = "",
            LastName = "Customer"
        };
        var invalidAddress = CreateAddress("Home");
        invalidAddress.PostalCode = "#$%";

        Assert.NotEmpty(Validate(invalidProfile));
        Assert.NotEmpty(Validate(invalidAddress));

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = await CreateContextAsync(connection);
        var customer = await AddCustomerAsync(context, "validation@test.com");
        var whitespace = CreateAddress("   ");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ProfileService(context).CreateAddressAsync(
                customer.UserId,
                whitespace));
    }

    private static IReadOnlyList<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            value,
            new ValidationContext(value),
            results,
            validateAllProperties: true);
        return results;
    }

    private static CreateAddressRequest CreateAddress(
        string label,
        bool isDefault = false) => new()
    {
        Label = label,
        AddressLine1 = "  10 Main Street  ",
        AddressLine2 = "  ",
        City = "Kandy",
        Province = "Central",
        PostalCode = "20000",
        Country = "Sri Lanka",
        IsDefault = isDefault
    };

    private static UpdateAddressRequest UpdateAddress(
        string label,
        bool isDefault = false) => new()
    {
        Label = label,
        AddressLine1 = "20 Updated Road",
        City = "Colombo",
        Province = "Western",
        PostalCode = "00100",
        Country = "Sri Lanka",
        IsDefault = isDefault
    };

    private static async Task<AppDbContext> CreateContextAsync(
        SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task<(int UserId, int CustomerId)> AddCustomerAsync(
        AppDbContext context,
        string email,
        string firstName = "Test",
        string lastName = "Customer")
    {
        var customer = new Customer
        {
            User = new User
            {
                Email = email,
                PasswordHash = "test-only-password-hash",
                FirstName = firstName,
                LastName = lastName,
                RoleId = 1,
                IsActive = true
            }
        };

        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return (customer.UserId, customer.Id);
    }

    private static ProfileController CreateController(
        IProfileService service,
        int? userId = null)
    {
        var claims = userId.HasValue
            ? new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId.Value.ToString())
            }
            : Array.Empty<Claim>();
        var identity = new ClaimsIdentity(
            claims,
            userId.HasValue ? "TestAuthentication" : null);

        return new ProfileController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }
}
