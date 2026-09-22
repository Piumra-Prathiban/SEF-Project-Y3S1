using SEF_Project.Api.Models;

namespace SEF_Project.Api.Services.Auth;

public interface IJwtService
{
    string GenerateToken(User user);
}