using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Services;
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

        services.Configure<IgdbOptions>(configuration.GetSection(IgdbOptions.SectionName));
        services.AddHttpClient<IIgdbClient, IgdbClient>();

        services.AddScoped<IGameCatalogService, GameCatalogService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILibraryService, LibraryService>();

        return services;
    }
}
