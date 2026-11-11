namespace Hashkeeper.Crypto;

/// <summary>
/// Generates the Vault Key and wraps (encrypts) it with the Wrap Key using
/// the shared XChaCha20-Poly1305 implementation.
/// </summary>
/// <remarks>
/// Implements section 6 of docs/crypto.md.
/// The Wrap Key is derived from the Master Key with HKDF and is supplied by the caller;
/// the Master Key itself is never passed to this class.
///
/// The serialized format is exactly 73 bytes:
/// <c>version (1) || nonce (24) || ciphertext (32) || Poly1305 tag (16)</c>.
/// Every production wrap uses a fresh random nonce. The fixed-nonce method is internal
/// and exists only for deterministic cryptographic test vectors.
///
/// Keys are raw 32-byte values. Callers own them and should clear them with
/// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/> when no longer needed.
/// </remarks>
public static class VaultKeyWrapper
{
    public const byte FormatVersion = 1;

    private const int KeySize = XChaCha20Poly1305Cipher.KeySize;
    private const int WrappedSize = 1
        + XChaCha20Poly1305Cipher.NonceSize
        + KeySize
        + XChaCha20Poly1305Cipher.TagSize;

    // Binds the ciphertext to its purpose and format version.
    // This value is ASCII, has no terminating null byte, and is authenticated but not encrypted.
    private static readonly byte[] AssociatedData = "hashkeeper:vault-key:v1"u8.ToArray();

    /// <summary>Generates a new cryptographically random 32-byte Vault Key.</summary>
    public static byte[] GenerateVaultKey() => RandomNumberGenerator.GetBytes(KeySize);

    /// <summary>Encrypts <paramref name="vaultKey"/> with <paramref name="wrapKey"/>.</summary>
    /// <param name="vaultKey">The 32-byte Vault Key to wrap.</param>
    /// <param name="wrapKey">The 32-byte Wrap Key derived from the Master Key.</param>
    /// <returns>The 73-byte value <c>version || nonce || ciphertext || tag</c>.</returns>
    /// <exception cref="ArgumentException">A key is not exactly 32 bytes long.</exception>
    public static byte[] Wrap(ReadOnlySpan<byte> vaultKey, ReadOnlySpan<byte> wrapKey)
    {
        // Check if vaultKey's lenght is correct
        // Only vaultKey's length is checked, because wrapKey is veryfied by XChaCha20-Poly1305Cypher class
        if (vaultKey.Length != KeySize)
        {
            throw new ArgumentException($"The key must be {KeySize} bytes long.", nameof(vaultKey));
        }

        var encryptedPayload = XChaCha20Poly1305Cipher.Encrypt(
            vaultKey,
            wrapKey,
            AssociatedData);

        if (encryptedPayload.Length != WrappedSize - 1)
        {
            throw new InvalidOperationException("The encrypted Vault Key payload has an unexpected length.");
        }

        var wrapped = new byte[WrappedSize];
        wrapped[0] = FormatVersion;
        encryptedPayload.CopyTo(wrapped.AsSpan(1));
        return wrapped;
    }

    /// <summary>Decrypts a wrapped Vault Key with <paramref name="wrapKey"/>.</summary>
    /// <param name="wrapped">The 73-byte wrapped Vault Key.</param>
    /// <param name="wrapKey">The 32-byte Wrap Key derived from the Master Key.</param>
    /// <returns>The original 32-byte Vault Key.</returns>
    /// <exception cref="ArgumentException">The Wrap Key is not exactly 32 bytes long.</exception>
    /// <exception cref="CryptographicException">
    /// The wrapped value has an invalid length or version, the Wrap Key is wrong,
    /// or authentication fails. These failures use one generic error message.
    /// </exception>
    public static byte[] Unwrap(ReadOnlySpan<byte> wrapped, ReadOnlySpan<byte> wrapKey)
    {
        // Check the length before reading byte zero. A malformed wrapped value must
        // produce the same public error as an unsupported version or failed authentication.
        if (wrapped.Length != WrappedSize || wrapped[0] != FormatVersion)
        {
            throw InvalidWrappedVaultKey();
        }

        try
        {
            // Skip the version byte. The shared primitive expects nonce || ciphertext || tag.
            return XChaCha20Poly1305Cipher.Decrypt(
                wrapped[1..],
                wrapKey,
                AssociatedData);
        }
        catch (CryptographicException)
        {
            // Do not reveal whether the key was wrong or the stored data was modified.
            throw InvalidWrappedVaultKey();
        }
    }

    private static CryptographicException InvalidWrappedVaultKey() =>
        new("Wrong Master Password or corrupted vault.");
}