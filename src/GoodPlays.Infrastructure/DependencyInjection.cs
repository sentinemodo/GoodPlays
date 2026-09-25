using GoodPlays.Infrastructure.Configuration;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.OpenCritic;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Infrastructure.Security;
using GoodPlays.Infrastructure.Services;
using GoodPlays.Infrastructure.Nintendo;
using GoodPlays.Infrastructure.Psn;
using GoodPlays.Infrastructure.Steam;
using GoodPlays.Infrastructure.Xbox;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoodPlays.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useLocalDevDatabase = false)
    {
        var connectionString = CloudConnectionResolver.ResolvePostgresConnection(configuration, useLocalDevDatabase);

        services.AddDbContext<GoodPlaysDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(GoodPlaysDbContext).Assembly.FullName)));

        services.Configure<IgdbOptions>(configuration.GetSection(IgdbOptions.SectionName));
        services.AddHttpClient<IIgdbClient, IgdbClient>();

        services.AddScoped<IGameCatalogService, GameCatalogService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IGameDetailService, GameDetailService>();
        services.AddScoped<IGameEnrichmentService, GameEnrichmentService>();
        services.AddScoped<ICoverRefreshService, CoverRefreshService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IAchievementSyncService, AchievementSyncService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IProfileService, ProfileService>();

        services.Configure<OpenCriticOptions>(configuration.GetSection(OpenCriticOptions.SectionName));
        services.AddHttpClient<IOpenCriticClient, OpenCriticClient>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IPlatformConnectionService, PlatformConnectionService>();
        services.AddScoped<ISteamSyncService, SteamSyncService>();
        services.AddScoped<IPsnSyncService, PsnSyncService>();
        services.AddScoped<IXboxSyncService, XboxSyncService>();
        services.AddScoped<ISwitchSyncService, SwitchSyncService>();
        services.AddScoped<IPlatformSyncRunService, PlatformSyncRunService>();
        services.AddScoped<ILibraryEntrySyncService, LibraryEntrySyncService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddDataProtection();
        services.AddSingleton<ITokenEncryptionService, DataProtectionTokenEncryptionService>();

        services.Configure<SteamOptions>(configuration.GetSection(SteamOptions.SectionName));
        services.AddHttpClient<ISteamClient, SteamClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.steampowered.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.Configure<PsnOptions>(configuration.GetSection(PsnOptions.SectionName));
        services.PostConfigure<PsnOptions>(PsnOptions.ApplyDefaults);
        services.AddHttpClient<IPsnClient, PsnClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

        services.AddHttpClient<IXboxClient, XboxClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<INintendoClient, NintendoClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", NintendoLogin.UserAgent);
        });

        return services;
    }
}
