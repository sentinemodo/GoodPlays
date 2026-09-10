using StackExchange.Redis;

namespace GoodPlays.Api.Configuration;

public static class RedisConnectionFactory
{
    public static ConfigurationOptions? BuildConfigurationOptions(IConfiguration configuration)
    {
        var redisUrl = configuration["REDIS_URL"];
        if (!string.IsNullOrWhiteSpace(redisUrl))
        {
            return BuildConfigurationOptions(redisUrl);
        }

        var explicitConnection = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(explicitConnection)
            && !IsLocalDevRedisDefault(explicitConnection))
        {
            return BuildConfigurationOptions(explicitConnection);
        }

        return null;
    }

    public static ConfigurationOptions BuildConfigurationOptions(string connection)
    {
        ConfigurationOptions options;

        if (connection.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            || connection.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            options = ConfigurationOptions.Parse(connection);
            var uri = new Uri(connection);
            if (uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase))
            {
                options.Ssl = true;
                options.SslHost = uri.Host;
            }
        }
        else
        {
            options = ConfigurationOptions.Parse(connection);
        }

        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 30000;
        options.SyncTimeout = 30000;
        options.AsyncTimeout = 30000;
        options.KeepAlive = 60;

        return options;
    }

    public static IConnectionMultiplexer? TryConnect(ConfigurationOptions options)
    {
        try
        {
            var multiplexer = ConnectionMultiplexer.Connect(options);
            multiplexer.GetDatabase().Ping();

            // Hangfire requires pub/sub; verify before registering the server.
            var subscriber = multiplexer.GetSubscriber();
            var probeChannel = RedisChannel.Literal("__goodplays_redis_probe__");
            subscriber.Subscribe(probeChannel, (_, _) => { });
            subscriber.Unsubscribe(probeChannel);

            return multiplexer;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsLocalDevRedisDefault(string connection)
        => connection.Equals("localhost:6379", StringComparison.OrdinalIgnoreCase);
}
