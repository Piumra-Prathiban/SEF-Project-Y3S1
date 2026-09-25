namespace SEF_Project.Api.Configuration;

/// <summary>
/// Bootstrap Administrator account, created on startup when it does not exist.
/// Supplied through local configuration (the gitignored <c>.env</c>) or any
/// ASP.NET Core configuration source, e.g.:
/// <code>
/// SeedAdmin__Email=admin@clothic.local
/// SeedAdmin__Password=...
/// </code>
/// </summary>
public class AdminSeedSettings
{
    public const string PlaceholderPassword = "change-me";

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
