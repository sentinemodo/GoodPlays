using GoodPlays.Api.Services;
using Microsoft.AspNetCore.Http;

namespace GoodPlays.Tests;

public class ClerkWebhookVerifierTests
{
    [Fact]
    public void TryVerify_RejectsMissingHeaders()
    {
        var headers = new HeaderDictionary();
        var verified = ClerkWebhookVerifier.TryVerify(headers, "{}", "whsec_" + Convert.ToBase64String([1, 2, 3]));
        Assert.False(verified);
    }
}
