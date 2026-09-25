using Microsoft.EntityFrameworkCore;
using SEF_Project.Api.Configuration;
using SEF_Project.Api.Models;
using SEF_Project.Api.Services.Auth;

namespace SEF_Project.Api.Data;

/// <summary>
/// Bootstraps the first Administrator account from configuration. Public
/// registration always creates Customers, so without this there is no way to
/// reach the staff/admin endpoints on a fresh database.
/// </summary>
/// <remarks>
/// This never modifies an existing account: if the configured email already
/// exists, its password and role are left exactly as they are.
/// </remarks>
public static class AdminUserSeeder
{
    public const string AdministratorRoleName = "Administrator";

    public static async Task SeedAsync(
        AppDbContext context,
        IPasswordService passwordService,
        AdminSeedSettings settings,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var email = settings.Email?.Trim().ToLowerInvariant();
        var password = settings.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "Admin seed skipped: set SeedAdmin__Email and SeedAdmin__Password "
                + "(see .env.example) to create an Administrator on startup.");

            return;
        }

        try
        {
            if (await context.Users.AnyAsync(
                    u => u.Email == email,
                    cancellationToken))
            {
                logger.LogInformation(
                    "Admin seed skipped: an account already exists for {Email}.",
                    email);

                return;
            }

            var role = await context.Roles.FirstOrDefaultAsync(
                r => r.Name == AdministratorRoleName,
                cancellationToken);

            if (role is null)
            {
                logger.LogWarning(
                    "Admin seed skipped: the {Role} role is missing. "
                    + "Run 'dotnet ef database update' first.",
                    AdministratorRoleName);

                return;
            }

            context.Users.Add(new User
            {
                Email = email,
                PasswordHash = passwordService.HashPassword(password),
                FirstName = string.IsNullOrWhiteSpace(settings.FirstName)
                    ? "Clothic"
                    : settings.FirstName.Trim(),
                LastName = string.IsNullOrWhiteSpace(settings.LastName)
                    ? "Admin"
                    : settings.LastName.Trim(),
                IsActive = true,
                RoleId = role.Id
            });

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Seeded the {Role} account {Email}.",
                AdministratorRoleName,
                email);

            if (password == AdminSeedSettings.PlaceholderPassword)
            {
                logger.LogWarning(
                    "The seeded Administrator still uses the example password. "
                    + "Change SeedAdmin__Password in .env before sharing this project.");
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Admin seed failed (the database may not be migrated yet). "
                + "Run 'dotnet ef database update' and restart the API.");
        }
    }
}
