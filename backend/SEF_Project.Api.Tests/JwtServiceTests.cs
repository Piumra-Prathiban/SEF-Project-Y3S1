using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SEF_Project.Api.Configuration;
using SEF_Project.Api.Models;
using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Tests;

public class JwtServiceTests
{
    [Fact]
    public void GenerateToken_ShouldContainExpectedUserClaims()
    {
        // Arrange
        var settings = Options.Create(new JwtSettings
        {
            Key = "ThisIsASecretKeyThatIsLongEnoughForHS256Testing1234567890",
            Issuer = "SEF-Project.Api",
            Audience = "SEF-Project.Clients",
            ExpiryMinutes = 60
        });

        var jwtService = new JwtService(settings);

        var user = new User
        {
            Id = 10,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = new Role
            {
                Id = 1,
                Name = "Customer"
            }
        };

        // Act
        var token = jwtService.GenerateToken(user);

        // Assert
        Assert.NotEmpty(token);

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal("SEF-Project.Api", jwtToken.Issuer);
        Assert.Contains(
            jwtToken.Audiences,
            audience => audience == "SEF-Project.Clients");

        Assert.Equal(
            "10",
            jwtToken.Claims.First(
                c => c.Type == ClaimTypes.NameIdentifier).Value);

        Assert.Equal(
            "test@example.com",
            jwtToken.Claims.First(
                c => c.Type == ClaimTypes.Email).Value);

        Assert.Equal(
            "Customer",
            jwtToken.Claims.First(
                c => c.Type == ClaimTypes.Role).Value);
    }
}