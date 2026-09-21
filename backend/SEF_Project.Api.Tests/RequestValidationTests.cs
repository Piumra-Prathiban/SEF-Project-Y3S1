using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Auth;

namespace SEF_Project.Api.Tests;

public class RequestValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            model,
            context,
            results,
            validateAllProperties: true);

        return results;
    }

    [Fact]
    public void RegisterRequest_ShouldBeValid_WhenAllFieldsAreCorrect()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "TestPassword123!",
            FirstName = "Test",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void RegisterRequest_ShouldBeInvalid_WhenEmailIsInvalid()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "not-an-email",
            Password = "TestPassword123!",
            FirstName = "Test",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(RegisterRequest.Email)));
    }

    [Fact]
    public void RegisterRequest_ShouldBeInvalid_WhenPasswordIsTooShort()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "short",
            FirstName = "Test",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(RegisterRequest.Password)));
    }

    [Fact]
    public void RegisterRequest_ShouldBeInvalid_WhenFirstNameIsMissing()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "TestPassword123!",
            FirstName = "",
            LastName = "Customer"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(RegisterRequest.FirstName)));
    }

    [Fact]
    public void LoginRequest_ShouldBeValid_WhenCredentialsAreCorrect()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "TestPassword123!"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void LoginRequest_ShouldBeInvalid_WhenEmailIsInvalid()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "invalid-email",
            Password = "TestPassword123!"
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(LoginRequest.Email)));
    }

    [Fact]
    public void LoginRequest_ShouldBeInvalid_WhenPasswordIsMissing()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = ""
        };

        // Act
        var results = Validate(request);

        // Assert
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(LoginRequest.Password)));
    }
}