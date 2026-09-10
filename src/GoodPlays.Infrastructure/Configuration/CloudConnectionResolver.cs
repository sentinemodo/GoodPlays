using Microsoft.Extensions.Configuration;

namespace GoodPlays.Infrastructure.Configuration;

public static class CloudConnectionResolver
{
    private const string DefaultPostgres =
        "Host=localhost;Port=5432;Database=goodplays;Username=goodplays;Password=goodplays";

    public static string ResolvePostgresConnection(IConfiguration configuration)
    {
        // Cloud env vars must win over localhost defaults baked into appsettings.json.
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return NormalizePostgresConnection(databaseUrl);
        }

        var explicitConnection = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(explicitConnection)
            && !IsLocalDevPostgresDefault(explicitConnection))
        {
            return NormalizePostgresConnection(explicitConnection);
        }

        return DefaultPostgres;
    }

    public static string? ResolveRedisConnection(IConfiguration configuration)
    {
        var redisUrl = configuration["REDIS_URL"];
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            return NormalizeRedisConnection(redisUrl);
        }

        var explicitConnection = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(explicitConnection)
            && !IsLocalDevRedisDefault(explicitConnection))
        {
            return NormalizeRedisConnection(explicitConnection);
        }

        return null;
    }

    private static bool IsLocalDevPostgresDefault(string connection)
        => connection.Contains("localhost", StringComparison.OrdinalIgnoreCase)
           || connection.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalDevRedisDefault(string connection)
        => connection.Equals("localhost:6379", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePostgresConnection(string connection)
    {
        if (!connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return connection;
        }

        if (connection.EndsWith("?sslmode", StringComparison.OrdinalIgnoreCase)
            || connection.EndsWith("&sslmode", StringComparison.OrdinalIgnoreCase))
        {
            connection += "=require";
        }

        var uri = new Uri(connection);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.IsDefaultPort ? 5432 : uri.Port;
        var sslMode = ParseSslMode(uri.Query);

        return $"Host={uri.Host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode}";
    }

    private static string ParseSslMode(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Require";
        }

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = part.Split('=', 2);
            if (segments.Length == 2
                && segments[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                return segments[1].ToLowerInvariant() switch
                {
                    "disable" => "Disable",
                    "prefer" => "Prefer",
                    "verify-ca" => "VerifyCA",
                    "verify-full" => "VerifyFull",
                    _ => "Require"
                };
            }
        }

        return "Require";
    }

    private static string NormalizeRedisConnection(string connection)
    {
        if (!connection.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            && !connection.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return connection;
        }

        var uri = new Uri(connection);
        var password = uri.UserInfo.Contains(':')
            ? uri.UserInfo[(uri.UserInfo.IndexOf(':') + 1)..]
            : uri.UserInfo;

        var useSsl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
        var hostPort = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";

        return string.IsNullOrWhiteSpace(password)
            ? $"{hostPort},ssl={useSsl.ToString().ToLowerInvariant()},abortConnect=false"
            : $"{hostPort},password={password},ssl={useSsl.ToString().ToLowerInvariant()},abortConnect=false";
    }
}
