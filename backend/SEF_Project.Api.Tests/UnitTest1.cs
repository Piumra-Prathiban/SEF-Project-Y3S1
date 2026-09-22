using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Tests;

public class PasswordServiceTests
{
    [Fact]
    public void HashPassword_ShouldCreateHashThatCanBeVerified()
    {
        // Arrange
        var passwordService = new PasswordService();
        var password = "TestPassword123!";

        // Act
        var passwordHash = passwordService.HashPassword(password);
        var result = passwordService.VerifyPassword(
            password,
            passwordHash);

        // Assert
        Assert.NotEmpty(passwordHash);
        Assert.NotEqual(password, passwordHash);
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalseForIncorrectPassword()
    {
        // Arrange
        var passwordService = new PasswordService();
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword123!";
        var passwordHash = passwordService.HashPassword(password);

        // Act
        var result = passwordService.VerifyPassword(
            wrongPassword,
            passwordHash);

        // Assert
        Assert.False(result);
    }
}