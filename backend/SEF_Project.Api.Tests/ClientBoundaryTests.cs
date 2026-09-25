using System.Text.RegularExpressions;

namespace SEF_Project.Api.Tests;

/// <summary>
/// Static, repository-wide proof that neither client codebase (React, Flutter)
/// can reach PostgreSQL or the Agentic AI service directly. Both clients may
/// only ever talk to the ASP.NET Core API over HTTP.
///
/// This scans source files rather than trusting a design description, so a
/// future change that adds a direct database/agent reference from a client
/// fails this test immediately.
/// </summary>
public class ClientBoundaryTests
{
    // Tokens that would indicate a client is bypassing the API: a database
    // driver/connection, raw SQL, or a reference to a server-only internal type.
    private static readonly (string Pattern, string Reason)[] ForbiddenPatterns =
    {
        (@"Npgsql", "an Npgsql (PostgreSQL) driver reference"),
        (@"postgres(ql)?://", "a PostgreSQL connection URI"),
        (@"Host\s*=\s*[\w.]+;\s*Database\s*=", "a raw ADO.NET connection string"),
        (@"\b5432\b", "the PostgreSQL default port"),
        // Deliberately not a generic "SELECT...FROM" scan: this codebase's
        // JSX legitimately contains <select> elements and words like "from"
        // (a date-range "From" field) within a few hundred characters of
        // each other, which made that heuristic noisy. INSERT/DELETE/DROP
        // have no such everyday English collision, so they stay precise.
        (@"\bINSERT\s+INTO\b", "a raw SQL INSERT statement"),
        (@"\bDELETE\s+FROM\b", "a raw SQL DELETE statement"),
        (@"\bDROP\s+TABLE\b", "a raw SQL DROP TABLE statement"),
        (@"SEF_Project\.Api\.AI\.InventoryPromotion", "a reference to the agent's internal namespace"),
        (@"InventoryPromotionAgentService", "a reference to the agent's internal service class"),
        (@"PromotionAgentToolRegistry", "a reference to the agent's internal tool registry"),
        (@"AppDbContext", "a reference to the API's internal EF Core DbContext"),
    };

    private static readonly string[] ExcludedDirectoryNames =
    {
        "node_modules", "dist", "build", ".dart_tool", ".git", "coverage",
    };

    // The Flutter client transport layer: the shopping feature's client plus
    // the retained orders/customer stacks, each of which owns one http.Client.
    // Every screen, widget, repository and controller must go through one of
    // these rather than the http package directly.
    private static readonly string[] FlutterHttpClientLayer =
    {
        Path.Combine("core", "api", "api_client.dart"),
        Path.Combine("services", "api_client.dart"),
        Path.Combine("services", "customer_api.dart"),
    };

    private static string RepoRoot { get; } = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SEF-Project.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException(
                "Could not locate the repository root (SEF-Project.sln) from the test output directory.");
    }

    private static IEnumerable<string> SourceFiles(string relativeDirectory, string searchPattern)
    {
        var root = Path.Combine(RepoRoot, relativeDirectory);

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Expected client source directory not found: {root}");
        }

        return Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => ExcludedDirectoryNames.Contains(segment)));
    }

    private static void AssertNoForbiddenReferences(IEnumerable<string> files)
    {
        var violations = new List<string>();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            foreach (var (pattern, reason) in ForbiddenPatterns)
            {
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                {
                    violations.Add($"{Path.GetRelativePath(RepoRoot, file)}: contains {reason} ('{pattern}').");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Client code must never reach PostgreSQL or the agent's internals directly:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void ReactSource_ShouldContainNoDirectDatabaseOrAgentAccess()
    {
        var files = SourceFiles("frontend/SEF-Project/src", "*.js")
            .Concat(SourceFiles("frontend/SEF-Project/src", "*.jsx"));

        AssertNoForbiddenReferences(files);
    }

    [Fact]
    public void FlutterSource_ShouldContainNoDirectDatabaseOrAgentAccess()
    {
        var files = SourceFiles("mobile/sef_project_mobile/sef_project/lib", "*.dart");

        AssertNoForbiddenReferences(files);
    }

    [Fact]
    public void ReactSource_ShouldSendEveryRequestThroughTheSingleApiClient()
    {
        // api.js is the one funnel through which every HTTP call is allowed to
        // pass; every other file must go through it rather than calling
        // fetch() itself, so there is exactly one place that ever leaves the
        // browser for the network.
        var offenders = SourceFiles("frontend/SEF-Project/src", "*.js")
            .Concat(SourceFiles("frontend/SEF-Project/src", "*.jsx"))
            .Where(file => !file.EndsWith(Path.Combine("services", "api.js"), StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\bfetch\s*\("))
            .Select(file => Path.GetRelativePath(RepoRoot, file))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Only services/api.js may call fetch() directly; found it in:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void FlutterSource_ShouldSendEveryRequestThroughTheSingleApiClient()
    {
        // The shopping screens go through core/api/api_client.dart; the retained
        // orders and customer stacks bring their own clients in services/. Every
        // other file must go through one of those clients rather than calling
        // the http package itself, so the client transport layer stays the only
        // place that ever leaves the app for the network.
        var offenders = SourceFiles("mobile/sef_project_mobile/sef_project/lib", "*.dart")
            .Where(file => !FlutterHttpClientLayer.Any(layer => file.EndsWith(layer, StringComparison.Ordinal)))
            .Where(file => Regex.IsMatch(File.ReadAllText(file), @"\bhttp\.(get|post|put|delete|patch|Client)\s*\("))
            .Select(file => Path.GetRelativePath(RepoRoot, file))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Only the Flutter client transport layer (core/api/api_client.dart, services/api_client.dart, "
            + "services/customer_api.dart) may call the http package directly; found it in:\n"
            + string.Join("\n", offenders));
    }
}
