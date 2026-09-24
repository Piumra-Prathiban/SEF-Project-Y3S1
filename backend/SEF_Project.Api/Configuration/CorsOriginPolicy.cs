namespace SEF_Project.Api.Configuration;

public static class CorsOriginPolicy
{
    public static string[] Normalize(IEnumerable<string>? configuredOrigins)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredOrigin in configuredOrigins ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(configuredOrigin))
            {
                continue;
            }

            var candidate = configuredOrigin.Trim();

            if (candidate == "*" ||
                !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                    uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrEmpty(uri.UserInfo) ||
                uri.AbsolutePath != "/" ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException(
                    "Every CORS entry must be an explicit HTTP or HTTPS origin.");
            }

            origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }

        if (origins.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one explicit CORS origin must be configured.");
        }

        return origins.ToArray();
    }
}
