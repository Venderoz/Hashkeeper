using System.Security.Cryptography;
using NSec.Cryptography;

namespace Hashkeeper.Crypto;

public static class HkdfSubkeyDeriver
{
    public const int KeySize = 32;

    private static readonly byte[] AuthHashInfo = "hashkeeper:v1:auth-hash"u8.ToArray();
    private static readonly byte[] WrapKeyInfo = "hashkeeper:v1:vault-key-wrap"u8.ToArray();

    private static readonly HkdfSha256 Hkdf = KeyDerivationAlgorithm.HkdfSha256;

    public static byte[] DeriveAuthHash(ReadOnlySpan<byte> masterKey) => Derive(masterKey, AuthHashInfo);

    public static byte[] DeriveWrapKey(ReadOnlySpan<byte> masterKey) => Derive(masterKey, WrapKeyInfo);

    private static byte[] Derive(ReadOnlySpan<byte> masterKey, byte[] info)
    {
        if (masterKey.Length != KeySize)
        {
            throw new ArgumentException($"The Master Key must be {KeySize} bytes long.", nameof(masterKey));
        }

        return Hkdf.DeriveBytes(masterKey, ReadOnlySpan<byte>.Empty, info, KeySize);
    }
}