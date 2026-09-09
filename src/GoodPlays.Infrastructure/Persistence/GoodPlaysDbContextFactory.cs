using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GoodPlays.Infrastructure.Persistence;

public class GoodPlaysDbContextFactory : IDesignTimeDbContextFactory<GoodPlaysDbContext>
{
    public GoodPlaysDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GoodPlaysDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=goodplays;Username=goodplays;Password=goodplays";

        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly(typeof(GoodPlaysDbContext).Assembly.FullName));

        return new GoodPlaysDbContext(optionsBuilder.Options);
    }
}
