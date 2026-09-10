using GoodPlays.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace GoodPlays.Tests;

public class CloudConnectionResolverTests
{
    [Fact]
    public void ResolvePostgresConnection_UsesExplicitConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=db.example;Database=goodplays"
            })
            .Build();

        var connection = CloudConnectionResolver.ResolvePostgresConnection(configuration);

        Assert.Equal("Host=db.example;Database=goodplays", connection);
    }

    [Fact]
    public void ResolvePostgresConnection_FallsBackToDatabaseUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "postgresql://user:pass@neon.example/goodplays?sslmode=require"
            })
            .Build();

        var connection = CloudConnectionResolver.ResolvePostgresConnection(configuration);

        Assert.Equal("Host=neon.example;Port=5432;Database=goodplays;Username=user;Password=pass;SSL Mode=Require", connection);
    }

    [Fact]
    public void ResolvePostgresConnection_PrefersDatabaseUrlOverLocalhostDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5432;Database=goodplays;Username=goodplays;Password=goodplays",
                ["DATABASE_URL"] = "postgresql://user:pass@neon.example/goodplays?sslmode=require"
            })
            .Build();

        var connection = CloudConnectionResolver.ResolvePostgresConnection(configuration);

        Assert.Equal("Host=neon.example;Port=5432;Database=goodplays;Username=user;Password=pass;SSL Mode=Require", connection);
    }

    [Fact]
    public void ResolvePostgresConnection_FixesTruncatedSslMode()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATABASE_URL"] = "postgresql://user:pass@neon.example/neondb?sslmode"
            })
            .Build();

        var connection = CloudConnectionResolver.ResolvePostgresConnection(configuration);

        Assert.Equal("Host=neon.example;Port=5432;Database=neondb;Username=user;Password=pass;SSL Mode=Require", connection);
    }

    [Fact]
    public void ResolveRedisConnection_PrefersRedisUrlOverLocalhostDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379",
                ["REDIS_URL"] = "rediss://default:secret-token@upstash.example:6379"
            })
            .Build();

        var connection = CloudConnectionResolver.ResolveRedisConnection(configuration);

        Assert.Equal("upstash.example:6379,password=secret-token,ssl=true,abortConnect=false", connection);
    }

    [Fact]
    public void ResolveRedisConnection_NormalizesRedissUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["REDIS_URL"] = "rediss://default:secret-token@upstash.example:6379"
            })
            .Build();

        var connection = CloudConnectionResolver.ResolveRedisConnection(configuration);

        Assert.Equal("upstash.example:6379,password=secret-token,ssl=true,abortConnect=false", connection);
    }
}
