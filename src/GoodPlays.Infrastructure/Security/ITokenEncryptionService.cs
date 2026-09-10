namespace GoodPlays.Infrastructure.Security;

public interface ITokenEncryptionService
{
    string Encrypt(string plaintext);

    string Decrypt(string ciphertext);
}
