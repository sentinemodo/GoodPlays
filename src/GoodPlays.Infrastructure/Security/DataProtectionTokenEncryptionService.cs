using Microsoft.AspNetCore.DataProtection;

namespace GoodPlays.Infrastructure.Security;

public sealed class DataProtectionTokenEncryptionService : ITokenEncryptionService
{
    private const string ProtectorPurpose = "GoodPlays.PlatformTokens.v1";
    private readonly IDataProtector _protector;

    public DataProtectionTokenEncryptionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(ProtectorPurpose);
    }

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertext);
        return _protector.Unprotect(ciphertext);
    }
}
