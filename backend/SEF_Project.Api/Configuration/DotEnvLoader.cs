namespace SEF_Project.Api.Configuration;

/// <summary>
/// Minimal .env reader so local secrets can live in a gitignored file instead
/// of appsettings. Keys use a double underscore for nesting:
/// <c>SeedAdmin__Email</c> becomes the configuration key
/// <c>SeedAdmin:Email</c>. Real environment variables always win over the file.
/// </summary>
public static class DotEnvLoader
{
    public static void Load(params string[] candidatePaths)
    {
        var path = candidatePaths.FirstOrDefault(File.Exists);

        if (path is null)
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');

            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();

            if (key.Length == 0)
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim().Trim('"');

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
