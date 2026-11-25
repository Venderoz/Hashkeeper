namespace Hashkeeper.Tray.Services.Storage;

/// <summary>
/// Persists the local vault: its metadata and its encrypted entries.
/// </summary>
/// <remarks>
/// The store never sees plaintext or keys. Encrypting and decrypting is the caller's job
/// (Hashkeeper.Crypto); the store only saves and loads the resulting bytes.
/// </remarks>
public interface ILocalVaultStore
{
    /// <summary>Full path of the vault file.</summary>
    string FilePath { get; }

    /// <summary>Whether a vault has been created, i.e. its metadata has been saved.</summary>
    bool VaultExists();

    /// <summary>Loads the vault metadata, or <see langword="null"/> if no vault exists yet.</summary>
    VaultMetadata? LoadMetadata();

    /// <summary>Saves the vault metadata, replacing any existing metadata.</summary>
    void SaveMetadata(VaultMetadata metadata);

    /// <summary>Loads one entry by ID, including a tombstone, or <see langword="null"/> if unknown.</summary>
    StoredEntry? LoadEntry(Guid id);

    /// <summary>Loads all entries, ordered by ID.</summary>
    /// <param name="includeDeleted">Whether to include tombstones (needed by sync).</param>
    IReadOnlyList<StoredEntry> LoadEntries(bool includeDeleted = false);

    /// <summary>Inserts the entry or replaces the stored entry with the same ID.</summary>
    void SaveEntry(StoredEntry entry);
}
