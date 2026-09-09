using Microsoft.Extensions.DependencyInjection;

namespace GoodPlays.Ml;

public static class DependencyInjection
{
    public static IServiceCollection AddMlServices(this IServiceCollection services)
    {
        services.AddSingleton<IRecommendationEngine, StubRecommendationEngine>();
        return services;
    }
}
