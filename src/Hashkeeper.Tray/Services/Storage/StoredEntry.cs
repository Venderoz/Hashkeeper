namespace Hashkeeper.Tray.Services.Storage;

/// <summary>
/// A vault entry as stored on disk: sync metadata in plain text, everything else encrypted
/// (docs/crypto.md §7.1).
/// </summary>
/// <param name="Id">Entry ID, also bound into the ciphertext's associated data.</param>
/// <param name="Version">Increases on every change; used by sync to resolve conflicts.</param>
/// <param name="UpdatedAt">Time of the last change. Stored with millisecond precision in UTC.</param>
/// <param name="IsDeleted">Tombstone: the entry was deleted but is kept so the deletion can sync.</param>
/// <param name="EncryptedData">Output of <c>CredentialEncryptor.Encrypt</c>; may be empty for tombstones.</param>
public sealed record StoredEntry(
    Guid Id,
    long Version,
    DateTimeOffset UpdatedAt,
    bool IsDeleted,
    byte[] EncryptedData);
