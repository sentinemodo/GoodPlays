using GoodPlays.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

namespace GoodPlays.Tests;

public class TokenEncryptionServiceTests
{
    [Fact]
    public void EncryptDecrypt_RoundTripsPlaintext()
    {
        var provider = new EphemeralDataProtectionProvider();
        var service = new DataProtectionTokenEncryptionService(provider);

        const string secret = "steam-web-api-key-abc123";
        var encrypted = service.Encrypt(secret);
        var decrypted = service.Decrypt(encrypted);

        Assert.NotEqual(secret, encrypted);
        Assert.Equal(secret, decrypted);
    }

    [Fact]
    public void Encrypt_RejectsBlankInput()
    {
        var provider = new EphemeralDataProtectionProvider();
        var service = new DataProtectionTokenEncryptionService(provider);

        Assert.Throws<ArgumentException>(() => service.Encrypt(" "));
    }
}
