using GoodPlays.Api.Extensions;
using GoodPlays.Api.Jobs;
using GoodPlays.Api.Services;
using GoodPlays.Infrastructure;
using GoodPlays.Infrastructure.Configuration;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Ml;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;

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

builder.Services.AddControllers();
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

var redisConnection = CloudConnectionResolver.ResolveRedisConnection(builder.Configuration);
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseRedisStorage(redisConnection));
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

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis");
}

builder.Services.AddTransient<StubRecurringJobs>();
builder.Services.AddTransient<ImportParseTextJob>();

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddSingleton<IImportJobScheduler, HangfireImportJobScheduler>();
}
else
{
    builder.Services.AddSingleton<IImportJobScheduler, InlineImportJobScheduler>();
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

if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(redisConnection))
{
    app.UseHangfireDashboard("/hangfire");
    StubRecurringJobs.Register();
}

app.Run();

public partial class Program;
