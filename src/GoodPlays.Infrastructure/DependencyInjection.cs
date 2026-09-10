using GoodPlays.Infrastructure.Configuration;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Infrastructure.Steam;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoodPlays.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = CloudConnectionResolver.ResolvePostgresConnection(configuration);

        services.AddDbContext<GoodPlaysDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(GoodPlaysDbContext).Assembly.FullName)));

        services.Configure<IgdbOptions>(configuration.GetSection(IgdbOptions.SectionName));
        services.AddHttpClient<IIgdbClient, IgdbClient>();

        services.AddScoped<IGameCatalogService, GameCatalogService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IPlatformConnectionService, PlatformConnectionService>();
        services.AddScoped<ISteamSyncService, SteamSyncService>();

        services.AddDataProtection();
        services.AddSingleton<ITokenEncryptionService, DataProtectionTokenEncryptionService>();

        services.Configure<SteamOptions>(configuration.GetSection(SteamOptions.SectionName));
        services.AddHttpClient<ISteamClient, SteamClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.steampowered.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
