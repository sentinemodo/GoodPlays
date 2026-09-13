using GoodPlays.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GoodPlays.Infrastructure.Persistence;

public class GoodPlaysDbContextFactory : IDesignTimeDbContextFactory<GoodPlaysDbContext>
{
    public GoodPlaysDbContext CreateDbContext(string[] args)
    {
        DotEnvLoader.TryLoad();

        var configurationValues = new Dictionary<string, string?>();
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            configurationValues["DATABASE_URL"] = databaseUrl;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            configurationValues["ConnectionStrings:Default"] = connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var resolvedConnection = CloudConnectionResolver.ResolvePostgresConnection(configuration);

        var optionsBuilder = new DbContextOptionsBuilder<GoodPlaysDbContext>();
        optionsBuilder.UseNpgsql(resolvedConnection, npgsql =>
            npgsql.MigrationsAssembly(typeof(GoodPlaysDbContext).Assembly.FullName));

        return new GoodPlaysDbContext(optionsBuilder.Options);
    }
}
