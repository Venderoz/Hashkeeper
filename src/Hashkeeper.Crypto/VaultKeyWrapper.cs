using System.Security.Cryptography;
using NSec.Cryptography;

namespace Hashkeeper.Crypto;

/// <summary>
/// Generates the Vault Key and wraps (encrypts) it with the Wrap Key using XChaCha20-Poly1305.
/// </summary>
/// <remarks>
/// Implements section 6 of docs/crypto.md. The Wrap Key is derived from the Master Key with
/// HKDF (section 5); the Master Key itself is never passed here. Wrapped format,
/// <see cref="WrappedSize"/> bytes: <c>version (1) || nonce (24) || ciphertext (32) || tag (16)</c>.
/// Every wrap uses a fresh random nonce. Keys are raw 32-byte values; callers own them and
/// should clear them with <see cref="CryptographicOperations.ZeroMemory"/> when done.
/// </remarks>
public static class VaultKeyWrapper
{
    public const int KeySize = 32;
    public const byte FormatVersion = 1;
    public const int WrappedSize = 1 + NonceSize + KeySize + TagSize;

    private const int NonceSize = 24;
    private const int TagSize = 16;
    private const int CiphertextOffset = 1 + NonceSize;

    // Binds the ciphertext to its purpose and format version, so a wrapped Vault Key
    // cannot be swapped with data encrypted for another purpose under the same key.
    private static readonly byte[] AssociatedData = "hashkeeper:vault-key:v1"u8.ToArray();

    private static readonly AeadAlgorithm Aead = AeadAlgorithm.XChaCha20Poly1305;

    /// <summary>Generates a new random 32-byte Vault Key.</summary>
    public static byte[] GenerateVaultKey() => RandomNumberGenerator.GetBytes(KeySize);

    /// <summary>Encrypts <paramref name="vaultKey"/> with <paramref name="wrapKey"/>.</summary>
    /// <exception cref="ArgumentException">A key is not <see cref="KeySize"/> bytes long.</exception>
    public static byte[] Wrap(ReadOnlySpan<byte> vaultKey, ReadOnlySpan<byte> wrapKey)
    {
        EnsureKeySize(vaultKey, nameof(vaultKey));
        using var key = ImportWrapKey(wrapKey);

        var wrapped = new byte[WrappedSize];
        wrapped[0] = FormatVersion;
        var nonce = wrapped.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        Aead.Encrypt(key, nonce, AssociatedData, vaultKey, wrapped.AsSpan(CiphertextOffset));
        return wrapped;
    }

    /// <summary>Decrypts a wrapped Vault Key with <paramref name="wrapKey"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="wrapKey"/> is not <see cref="KeySize"/> bytes long.</exception>
    /// <exception cref="CryptographicException">
    /// The Wrap Key is wrong, or the wrapped data is corrupted, truncated or in an unsupported format.
    /// </exception>
    public static byte[] Unwrap(ReadOnlySpan<byte> wrapped, ReadOnlySpan<byte> wrapKey)
    {
        if (wrapped.Length != WrappedSize)
        {
            throw new CryptographicException("The wrapped Vault Key has an invalid length.");
        }

        if (wrapped[0] != FormatVersion)
        {
            throw new CryptographicException($"Unsupported wrapped Vault Key version: {wrapped[0]}.");
        }

        using var key = ImportWrapKey(wrapKey);

        var vaultKey = new byte[KeySize];
        if (!Aead.Decrypt(key, wrapped.Slice(1, NonceSize), AssociatedData, wrapped[CiphertextOffset..], vaultKey))
        {
            throw new CryptographicException("Wrong Wrap Key or corrupted Vault Key.");
        }

        return vaultKey;
    }

    private static Key ImportWrapKey(ReadOnlySpan<byte> wrapKey)
    {
        EnsureKeySize(wrapKey, nameof(wrapKey));
        return Key.Import(Aead, wrapKey, KeyBlobFormat.RawSymmetricKey);
    }

    private static void EnsureKeySize(ReadOnlySpan<byte> key, string paramName)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"The key must be {KeySize} bytes long.", paramName);
        }
    }
}
