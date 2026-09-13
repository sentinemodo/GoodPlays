using System.Text.Json.Serialization;
using GoodPlays.Api.Configuration;
using GoodPlays.Api.Extensions;
using GoodPlays.Api.Jobs;
using GoodPlays.Api.Services;
using GoodPlays.Infrastructure;
using GoodPlays.Infrastructure.Configuration;
using GoodPlays.Infrastructure.Metadata;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Ml;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;

DotEnvLoader.TryLoad();

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GoodPlays API",
        Version = "v1",
        Description = "Goodreads-for-games REST API"
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMlServices(builder.Configuration);

IConnectionMultiplexer? redisMultiplexer = null;
var redisOptions = RedisConnectionFactory.BuildConfigurationOptions(builder.Configuration);
if (redisOptions is not null)
{
    redisMultiplexer = RedisConnectionFactory.TryConnect(redisOptions);
    if (redisMultiplexer is null)
    {
        Log.Warning("Redis unavailable; import jobs will run inline");
    }
}

if (redisMultiplexer is not null)
{
    builder.Services.AddSingleton(redisMultiplexer);
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseRedisStorage(redisMultiplexer));
    builder.Services.AddHangfireServer();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddClerkAuthentication(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteDev", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());

    var productionOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["https://sentinemodo.github.io"];
    options.AddPolicy("Production", policy =>
        policy.WithOrigins(productionOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var connectionString = CloudConnectionResolver.ResolvePostgresConnection(builder.Configuration);
var healthChecks = builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres")
    .AddCheck("ml", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Research recommendation engine ready (Phase 1)"));

if (redisMultiplexer is not null)
{
    healthChecks.AddRedis(redisMultiplexer, name: "redis");
}

builder.Services.AddTransient<StubRecurringJobs>();
builder.Services.AddTransient<ImportParseTextJob>();
builder.Services.AddTransient<SteamSyncJob>();
builder.Services.AddTransient<PsnSyncJob>();

if (redisMultiplexer is not null)
{
    builder.Services.AddSingleton<IImportJobScheduler, HangfireImportJobScheduler>();
    builder.Services.AddSingleton<ISteamSyncJobScheduler, HangfireSteamSyncJobScheduler>();
    builder.Services.AddSingleton<IPsnSyncJobScheduler, HangfirePsnSyncJobScheduler>();
}
else
{
    builder.Services.AddSingleton<IImportJobScheduler, InlineImportJobScheduler>();
    builder.Services.AddScoped<ISteamSyncJobScheduler, InlineSteamSyncJobScheduler>();
    builder.Services.AddScoped<IPsnSyncJobScheduler, InlinePsnSyncJobScheduler>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "GoodPlays API v1"));
}

app.UseSerilogRequestLogging();
app.UseCors(app.Environment.IsDevelopment() ? "ViteDev" : "Production");

app.UseAuthentication();
app.UseAuthorization();

if (string.Equals(app.Configuration["RunDbMigrations"], "true", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GoodPlaysDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();
app.MapHealthChecks("/health");

var igdbOptions = app.Services.GetRequiredService<IOptions<IgdbOptions>>().Value;
if (!igdbOptions.IsConfigured)
{
    Log.Warning("IGDB credentials not configured — game covers and metadata enrichment are disabled");
}

if (app.Environment.IsDevelopment() && redisMultiplexer is not null)
{
    app.UseHangfireDashboard("/hangfire");
    StubRecurringJobs.Register();
}

app.Run();

public partial class Program;
