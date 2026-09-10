using GoodPlays.Ml.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoodPlays.Ml;

public static class DependencyInjection
{
    public static IServiceCollection AddMlServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.AddHttpClient<ILlmClient, OpenAiLlmClient>();
        services.AddScoped<IRecommendationEngine, ResearchRecommendationEngine>();
        return services;
    }
}
