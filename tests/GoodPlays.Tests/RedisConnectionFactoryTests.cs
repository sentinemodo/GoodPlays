using GoodPlays.Api.Configuration;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace GoodPlays.Tests;

public class RedisConnectionFactoryTests
{
    [Fact]
    public void BuildConfigurationOptions_SetsSslHostForUpstashUrl()
    {
        var options = RedisConnectionFactory.BuildConfigurationOptions(
            "rediss://default:secret-token@verified-grubworm.example.upstash.io:6379");

        Assert.True(options.Ssl);
        Assert.Equal("verified-grubworm.example.upstash.io", options.SslHost);
        Assert.False(options.AbortOnConnectFail);
        Assert.Equal(30000, options.ConnectTimeout);
    }

    [Fact]
    public void BuildConfigurationOptions_FromConfiguration_PrefersRedisUrl()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "localhost:6379",
                ["REDIS_URL"] = "rediss://default:secret-token@verified-grubworm.example.upstash.io:6379"
            })
            .Build();

        var options = RedisConnectionFactory.BuildConfigurationOptions(configuration);

        Assert.NotNull(options);
        Assert.Equal("verified-grubworm.example.upstash.io", options!.SslHost);
    }
}
