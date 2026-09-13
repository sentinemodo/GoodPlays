using System.Text;
using GoodPlays.Infrastructure.Psn;

namespace GoodPlays.Tests;

public class PsnOAuthDefaultsTests
{
    [Fact]
    public void BasicAuthParameter_MatchesKnownMobileAppCredentials()
    {
        var expected = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{PsnOAuthDefaults.ClientId}:{PsnOAuthDefaults.ClientSecret}"));

        Assert.Equal(PsnOAuthDefaults.BasicAuthParameter, expected);
    }
}
