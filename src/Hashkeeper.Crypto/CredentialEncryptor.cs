namespace Hashkeeper.Crypto;

/// <summary>
/// Encrypts and decrypts credential data using the vault key.
/// Each encrypted value is bound to its credential entry ID through AEAD associated data.
/// </summary>
public static class CredentialEncryptor
{
    // Format: version || nonce || ciphertext || authentication tag.
    private const byte FormatVersion = 1;

    // 32 hexagonal numbers + 4 dashes
    private const byte EntryIdSize = 36;

    private const string AssociatedDataPrefix = "hashkeeper:credential:v1:";

    /// <summary>
    /// Encrypts credential data with the vault key.
    /// </summary>
    public static byte[] Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> vaultKey,
        string entryId)
    {
        ValidateEntryId(entryId);

        byte[] associatedData = BuildAssociatedData(entryId);
        byte[] encryptedPayload = XChaCha20Poly1305Cipher.Encrypt(
            plaintext,
            vaultKey,
            associatedData);

        var result = new byte[1 + encryptedPayload.Length];
        result[0] = FormatVersion;
        encryptedPayload.CopyTo(result, 1);

        return result;
    }

    /// <summary>
    /// Decrypts credential data. The entry ID must match the one used for encryption.
    /// </summary>
    /// <exception cref="CryptographicException">
    /// The data has an unsupported version, an invalid length, or authentication fails.
    /// </exception>
    public static byte[] Decrypt(
        ReadOnlySpan<byte> encrypted,
        ReadOnlySpan<byte> vaultKey,
        string entryId)
    {
        ValidateEntryId(entryId);

        if (encrypted.IsEmpty || encrypted[0] != FormatVersion)
        {
            throw new CryptographicException(
                $"Given encrypted data is invalid.");
        }

        byte[] associatedData = BuildAssociatedData(entryId);

        // The shared cipher expects nonce || ciphertext || tag,
        // without the format-version byte owned by this class.
        return XChaCha20Poly1305Cipher.Decrypt(
            encrypted[1..],
            vaultKey,
            associatedData);
    }

    private static byte[] BuildAssociatedData(string entryId)
    {
        return Encoding.UTF8.GetBytes(AssociatedDataPrefix + entryId);
    }

    private static void ValidateEntryId(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId) ||
            entryId.Length != EntryIdSize ||
            !Guid.TryParseExact(entryId, "D", out Guid parsedId) ||
            parsedId.ToString("D") != entryId)
        {
            throw new ArgumentException(
                "Given entry id is incorrect.",
                nameof(entryId));
        }
    }
}
