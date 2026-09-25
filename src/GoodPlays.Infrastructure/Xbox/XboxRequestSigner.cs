using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GoodPlays.Infrastructure.Xbox;

public sealed class XboxRequestSigner
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public Dictionary<string, string> ProofKey
    {
        get
        {
            var parameters = _key.ExportParameters(false);
            return new Dictionary<string, string>
            {
                ["use"] = "sig",
                ["alg"] = "ES256",
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = EncodeCoord(parameters.Q.X),
                ["y"] = EncodeCoord(parameters.Q.Y)
            };
        }
    }

    public string Sign(string method, string pathAndQuery, byte[]? body = null, string authorization = "")
    {
        body ??= [];
        var timestamp = DateTime.UtcNow.ToFileTimeUtc();
        var version = new byte[4];
        version[3] = 1;
        var timestampBytes = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            timestampBytes[i] = (byte)((timestamp >> (8 * (7 - i))) & 0xFF);
        }

        var payload = Concat(
            version,
            [0],
            timestampBytes,
            [0],
            Encoding.ASCII.GetBytes(method.ToUpperInvariant()),
            [0],
            Encoding.ASCII.GetBytes(pathAndQuery),
            [0],
            Encoding.ASCII.GetBytes(authorization),
            [0],
            body.Length > 8192 ? body[..8192] : body,
            [0]);

        var digest = SHA256.HashData(payload);
        var signature = _key.SignHash(digest, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String(Concat(version, timestampBytes, signature));
    }

    public string ProofKeyJson() => JsonSerializer.Serialize(ProofKey);

    private static string EncodeCoord(byte[]? coord)
    {
        var padded = new byte[32];
        if (coord is { Length: > 0 })
        {
            var copyLength = Math.Min(32, coord.Length);
            Buffer.BlockCopy(coord, 0, padded, 32 - copyLength, copyLength);
        }

        return Convert.ToBase64String(padded).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var length = parts.Sum(part => part.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
