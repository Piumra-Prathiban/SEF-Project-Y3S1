namespace SEF_Project.Api.Services.Marketing;

internal static class MarketingDates
{
    // PostgreSQL "timestamp with time zone" columns require UTC. Dates sent
    // without an offset are treated as UTC.
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static bool IsDescending(string? sortDirection) =>
        !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
}
