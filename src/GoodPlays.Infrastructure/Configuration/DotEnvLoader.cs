using System.Text.RegularExpressions;

namespace GoodPlays.Infrastructure.Configuration;

public static partial class DotEnvLoader
{
    [GeneratedRegex(@"^\s*([^#=]+?)=(.*)$")]
    private static partial Regex EnvLineRegex();

    /// <summary>
    /// Loads KEY=VALUE pairs from a .env file into the process environment.
    /// Existing environment variables are not overwritten.
    /// </summary>
    public static bool TryLoad(string? startDirectory = null, string fileName = ".env")
    {
        var path = FindEnvFile(startDirectory, fileName);
        if (path is null)
        {
            return false;
        }

        ApplyFile(path);
        return true;
    }

    public static string? FindEnvFile(string? startDirectory, string fileName)
    {
        var directory = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    internal static void ApplyFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var match = EnvLineRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups[1].Value.Trim();
            var value = Unquote(match.Groups[2].Value.Trim());
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1];
        }

        return value;
    }
}
