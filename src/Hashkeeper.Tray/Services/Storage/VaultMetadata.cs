namespace Hashkeeper.Tray.Services.Storage;

/// <summary>
/// Everything needed to unlock the vault, stored once per vault (docs/crypto.md §4.3 and §6).
/// None of it is secret on its own: the Vault Key is only stored wrapped.
/// </summary>
/// <param name="Kdf">KDF name, <c>"argon2id"</c> in v1.</param>
/// <param name="KdfIterations">Argon2id iterations (t).</param>
/// <param name="KdfMemoryKiB">Argon2id memory size in KiB (m).</param>
/// <param name="KdfParallelism">Argon2id parallelism (p), always 1 in v1.</param>
/// <param name="KdfSalt">The 16-byte Argon2id salt.</param>
/// <param name="WrappedVaultKey">The Vault Key wrapped with the Wrap Key (73 bytes in v1).</param>
public sealed record VaultMetadata(
    string Kdf,
    int KdfIterations,
    int KdfMemoryKiB,
    int KdfParallelism,
    byte[] KdfSalt,
    byte[] WrappedVaultKey);
