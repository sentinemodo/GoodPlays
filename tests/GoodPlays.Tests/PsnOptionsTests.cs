using GoodPlays.Infrastructure.Psn;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GoodPlays.Tests;

public class PsnOptionsTests
{
    [Fact]
    public void ApplyDefaults_RestoresKnownMobileAppCredentialsWhenEmpty()
    {
        var options = new PsnOptions { ClientId = "", ClientSecret = "" };

        PsnOptions.ApplyDefaults(options);

        Assert.Equal(PsnOAuthDefaults.ClientId, options.ClientId);
        Assert.Equal(PsnOAuthDefaults.ClientSecret, options.ClientSecret);
        Assert.Equal(PsnOAuthDefaults.RedirectUri, options.RedirectUri);
        Assert.Equal(PsnOAuthDefaults.Scope, options.Scope);
    }

    [Fact]
    public void ConfigurationBinding_WithEmptyAppsettingsValues_AppliesDefaultsViaPostConfigure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Psn:ClientId"] = "",
                ["Psn:ClientSecret"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<PsnOptions>(configuration.GetSection(PsnOptions.SectionName));
        services.PostConfigure<PsnOptions>(PsnOptions.ApplyDefaults);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<PsnOptions>>().Value;

        Assert.Equal(PsnOAuthDefaults.ClientId, options.ClientId);
        Assert.Equal(PsnOAuthDefaults.ClientSecret, options.ClientSecret);
    }
}
