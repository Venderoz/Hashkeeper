namespace Hashkeeper.Tray.Services.Vault;

/// <summary>Reads the unlocked Vault Key without copying it out of the session.</summary>
public delegate T VaultKeyFunc<out T>(ReadOnlySpan<byte> vaultKey);

/// <summary>
/// Creates, unlocks and locks the local vault, and holds the Vault Key in memory while it is unlocked
/// (operations in docs/crypto.md §10).
/// </summary>
/// <remarks>
/// <see cref="CreateVault"/> and <see cref="TryUnlock"/> run Argon2id, which takes a noticeable
/// moment (64 MiB of memory); call them from a background thread, not the UI thread.
/// </remarks>
public interface IVaultSession
{
    /// <summary>Minimum Master Password length, in characters.</summary>
    const int MinimumPasswordLength = 12;

    /// <summary>Raised after the vault is unlocked or locked.</summary>
    event EventHandler? LockStateChanged;

    /// <summary>Whether a vault has been created on this device.</summary>
    bool VaultExists { get; }

    /// <summary>Whether the Vault Key is currently in memory.</summary>
    bool IsUnlocked { get; }

    /// <summary>Creates a new vault protected by <paramref name="masterPassword"/> and leaves it unlocked.</summary>
    /// <exception cref="ArgumentException">The password is shorter than <see cref="MinimumPasswordLength"/>.</exception>
    /// <exception cref="InvalidOperationException">A vault already exists.</exception>
    void CreateVault(string masterPassword);

    /// <summary>Unlocks the vault.</summary>
    /// <returns>
    /// <see langword="false"/> if the password is wrong or the vault data is corrupted or invalid.
    /// The two cases are deliberately not distinguished (docs/crypto.md §6.2).
    /// </returns>
    /// <exception cref="InvalidOperationException">No vault exists yet.</exception>
    bool TryUnlock(string masterPassword);

    /// <summary>Clears the Vault Key from memory. Does nothing if the vault is already locked.</summary>
    void Lock();

    /// <summary>Runs <paramref name="action"/> with the Vault Key, e.g. to encrypt or decrypt an entry.</summary>
    /// <exception cref="InvalidOperationException">The vault is locked.</exception>
    T WithVaultKey<T>(VaultKeyFunc<T> action);
}
