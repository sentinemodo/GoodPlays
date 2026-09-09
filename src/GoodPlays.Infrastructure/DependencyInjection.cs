using GoodPlays.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoodPlays.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=goodplays;Username=goodplays;Password=goodplays";

        services.AddDbContext<GoodPlaysDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(GoodPlaysDbContext).Assembly.FullName)));

        return services;
    }
}
