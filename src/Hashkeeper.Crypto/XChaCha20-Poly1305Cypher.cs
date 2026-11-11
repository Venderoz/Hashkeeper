[assembly: InternalsVisibleTo("Hashkeeper.Crypto.Tests")]

namespace Hashkeeper.Crypto;

/// <summary>
/// Provides the shared XChaCha20-Poly1305 authenticated-encryption primitive
/// used by Hashkeeper's purpose-specific encryptors and key wrappers.
/// </summary>
/// <remarks>
/// The serialized result produced by <see cref="Encrypt"/> is:
/// <c>nonce (24 bytes) || ciphertext || Poly1305 tag (16 bytes)</c>.
/// It deliberately does not include a format-version byte; callers add and
/// validate the version byte required by their respective data format.
///
/// Production encryption always generates a fresh random nonce internally.
/// The fixed-nonce entry point is internal and exists only for deterministic
/// cryptographic test vectors. Production callers must use <see cref="Encrypt"/>.
/// Keys and returned plaintext are owned by the caller and should be cleared with
/// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/> when no longer needed.
/// </remarks>
internal static class XChaCha20Poly1305Cipher
{
    internal const int KeySize = 32;
    internal const int NonceSize = 24;
    internal const int TagSize = 16;
    private const int Overhead = NonceSize + TagSize;

    private static readonly AeadAlgorithm Algorithm = AeadAlgorithm.XChaCha20Poly1305;

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with a 32-byte key and associated data.
    /// A fresh 24-byte nonce is generated for every call.
    /// </summary>
    /// <param name="plaintext">The bytes to encrypt. Empty plaintext is allowed.</param>
    /// <param name="keyBytes">The raw 32-byte XChaCha20-Poly1305 key.</param>
    /// <param name="associatedData">Authenticated, but not encrypted, context data.</param>
    /// <returns><c>nonce || ciphertext || tag</c>.</returns>
    /// <exception cref="ArgumentException">The key is not exactly 32 bytes long.</exception>
    internal static byte[] Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> keyBytes,
        ReadOnlySpan<byte> associatedData)
    {
        var encrypted = new byte[checked(Overhead + plaintext.Length)];
        var nonce = encrypted.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        var ciphertextAndTag = encrypted.AsSpan(NonceSize);

        using var key = ImportKey(keyBytes);
        Algorithm.Encrypt(key, nonce, associatedData, plaintext, ciphertextAndTag);

        return encrypted;
    }

    /// <summary>
    /// Authenticates and decrypts data serialized as
    /// <c>nonce || ciphertext || tag</c>.
    /// </summary>
    /// <param name="encrypted">The nonce, ciphertext and authentication tag.</param>
    /// <param name="keyBytes">The raw 32-byte XChaCha20-Poly1305 key.</param>
    /// <param name="associatedData">The same associated data used during encryption.</param>
    /// <returns>The plaintext, only if authentication succeeds.</returns>
    /// <exception cref="ArgumentException">The key is not exactly 32 bytes long.</exception>
    /// <exception cref="CryptographicException">
    /// The input is too short or authentication fails (for example, because the
    /// key, associated data or ciphertext is wrong).
    /// </exception>
    internal static byte[] Decrypt(
        ReadOnlySpan<byte> encrypted,
        ReadOnlySpan<byte> keyBytes,
        ReadOnlySpan<byte> associatedData)
    {
        if (encrypted.Length < Overhead)
        {
            throw new CryptographicException("Encrypted data has an invalid length.");
        }

        var plaintext = new byte[encrypted.Length - Overhead];
        try
        {
            using var key = ImportKey(keyBytes);
            var nonce = encrypted[..NonceSize];
            var ciphertextAndTag = encrypted[NonceSize..];

            if (!Algorithm.Decrypt(
                    key,
                    nonce,
                    associatedData,
                    ciphertextAndTag,
                    plaintext))
            {
                throw new CryptographicException(
                    "Encrypted data is invalid or authentication failed.");
            }

            return plaintext;
        }
        catch
        {
            // Never leave unauthenticated or partially written plaintext in memory.
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
    }

    private static Key ImportKey(ReadOnlySpan<byte> keyBytes)
    {
        if (keyBytes.Length != KeySize)
        {
            throw new ArgumentException(
                $"The key must be {KeySize} bytes long.",
                nameof(keyBytes));
        }

        return Key.Import(Algorithm, keyBytes, KeyBlobFormat.RawSymmetricKey);
    }
}

