using GoodPlays.Api.Extensions;
using GoodPlays.Api.Jobs;
using GoodPlays.Infrastructure;
using GoodPlays.Infrastructure.Persistence;
using GoodPlays.Ml;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

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
        Description = "Goodreads-for-games REST API (Phase 0 skeleton)"
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMlServices();

var redisConnection = builder.Configuration["Redis:ConnectionString"];
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

builder.Services.AddClerkAuthentication(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteDev", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var connectionString = builder.Configuration.GetConnectionString("Default");
var healthChecks = builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString ?? "Host=localhost;Port=5432;Database=goodplays;Username=goodplays;Password=goodplays", name: "postgres")
    .AddCheck("ml", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("ML.NET stub ready (Phase 0)"));

if (!string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis");
}

builder.Services.AddTransient<StubRecurringJobs>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "GoodPlays API v1"));
}

app.UseSerilogRequestLogging();
app.UseCors("ViteDev");

if (ClerkAuthenticationExtensions.IsClerkConfigured(app.Configuration))
{
    app.UseAuthentication();
    app.UseAuthorization();
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
