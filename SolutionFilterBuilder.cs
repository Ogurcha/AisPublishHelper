using System.Text.Json;
using System.Text.RegularExpressions;

namespace AisPublishHelper;

internal static class SolutionFilterBuilder
{
    private static readonly Regex ProjectLine = new(
        @"^Project\(""(?<type>[^""]+)""\)\s*=\s*""(?<name>[^""]+)"",\s*""(?<path>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ProjectExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj",
        ".vbproj",
        ".fsproj",
        ".wixproj",
        ".vcxproj",
        ".sqlproj",
        ".njsproj"
    };

    public static string? CreateIfNeeded(string solutionPath, IReadOnlyCollection<string> excludedNames)
    {
        if (excludedNames.Count == 0)
        {
            return null;
        }

        var excluded = excludedNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (excluded.Count == 0)
        {
            return null;
        }

        var projects = ParseProjects(solutionPath);
        var skipped = projects.Where(project => IsExcluded(project, excluded)).ToList();
        if (skipped.Count == 0)
        {
            return null;
        }

        foreach (var project in skipped)
        {
            ConsoleUi.Info($"Excluding from build: {project.Name} ({project.RelativePath})");
        }

        var included = projects
            .Where(project => !IsExcluded(project, excluded))
            .Select(project => project.RelativePath.Replace('/', '\\'))
            .ToList();

        var filterPath = Path.Combine(
            Path.GetDirectoryName(solutionPath)!,
            Path.GetFileNameWithoutExtension(solutionPath) + ".AisPublishHelper.slnf");

        var payload = new
        {
            solution = new
            {
                path = Path.GetFileName(solutionPath),
                projects = included
            }
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filterPath, json);
        ConsoleUi.Info($"Solution filter: {filterPath}");
        return filterPath;
    }

    private static bool IsExcluded(ProjectEntry project, HashSet<string> excluded)
    {
        return excluded.Contains(project.Name)
               || excluded.Contains(Path.GetFileNameWithoutExtension(project.RelativePath))
               || excluded.Contains(Path.GetFileName(project.RelativePath));
    }

    private static List<ProjectEntry> ParseProjects(string solutionPath)
    {
        var result = new List<ProjectEntry>();
        foreach (var line in File.ReadLines(solutionPath))
        {
            var match = ProjectLine.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var relativePath = match.Groups["path"].Value;
            var extension = Path.GetExtension(relativePath);
            if (!ProjectExtensions.Contains(extension))
            {
                continue;
            }

            result.Add(new ProjectEntry(match.Groups["name"].Value, relativePath));
        }

        return result;
    }

    private sealed record ProjectEntry(string Name, string RelativePath);
}
