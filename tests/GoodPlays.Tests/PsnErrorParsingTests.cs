using GoodPlays.Infrastructure.Psn;

namespace GoodPlays.Tests;

public class PsnErrorParsingTests
{
    [Fact]
    public void TryReadErrorMessage_ParsesOAuthInvalidClientResponse()
    {
        const string json =
            """{"error":"invalid_client","error_description":"Bad client credentials","error_code":4102}""";

        var message = PsnErrorParser.TryReadMessage(json);

        Assert.Equal("Bad client credentials", message);
    }

    [Fact]
    public void TryReadErrorMessage_ParsesNestedPsnApiErrorObject()
    {
        const string json =
            """{"error":{"referenceId":"abc","code":2281473,"message":"Bad Request (path: accountId)"}}""";

        var message = PsnErrorParser.TryReadMessage(json);

        Assert.Equal("Bad Request (path: accountId)", message);
    }
}
