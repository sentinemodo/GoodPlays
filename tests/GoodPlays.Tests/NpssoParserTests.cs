using GoodPlays.Infrastructure.Psn;

namespace GoodPlays.Tests;

public class NpssoParserTests
{
    [Fact]
    public void Normalize_ReturnsRawTokenWhenAlreadyPlain()
    {
        const string token = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUV";
        Assert.Equal(token, NpssoParser.Normalize(token));
    }

    [Fact]
    public void Normalize_ExtractsTokenFromJsonResponse()
    {
        const string token = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUV";
        var json = $$"""{"npsso":"{{token}}"}""";

        Assert.Equal(token, NpssoParser.Normalize(json));
    }

    [Fact]
    public void Normalize_StripsSurroundingQuotes()
    {
        const string token = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUV";
        Assert.Equal(token, NpssoParser.Normalize($"\"{token}\""));
    }
}
