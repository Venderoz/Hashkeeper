using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace Hashkeeper.Crypto;

/// <summary>
/// Derives the Master Key from the Master Password and a salt with Argon2id.
/// </summary>
/// <remarks>
/// Implements section 4 of docs/crypto.md. The password is normalised to Unicode NFKC
/// and encoded as UTF-8 before hashing. Parallelism is always 1.
///
/// Unlock must use the iterations, memory size and salt stored with the vault, not the
/// current defaults. Values below the v1 minimums are rejected so a malicious server
/// cannot downgrade the KDF, and values above the maximums are rejected so it cannot
/// make unlock hang or run out of memory.
///
/// The returned Master Key is owned by the caller, who should clear it with
/// <see cref="CryptographicOperations.ZeroMemory(Span{byte})"/> once the subkeys are derived.
/// </remarks>
public static class Argon2idMasterKeyDeriver
{
    public const int KeySize = 32;
    public const int SaltSize = 16;

    public const int DefaultIterations = 3;
    public const int DefaultMemoryKiB = 65536;

    // Equal to the v1 defaults for now. Defaults may rise later; the minimums stay so older vaults still unlock.
    public const int MinIterations = DefaultIterations;
    public const int MinMemoryKiB = DefaultMemoryKiB;

    public const int MaxIterations = 16;
    public const int MaxMemoryKiB = 1024 * 1024;

    /// <summary>Generates a new random 16-byte salt.</summary>
    /// <remarks>Call this when the vault is created and on every Master Password change.</remarks>
    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>Derives the 32-byte Master Key.</summary>
    /// <param name="masterPassword">The Master Password as typed. It is normalised to NFKC but never trimmed.</param>
    /// <param name="salt">The 16-byte salt stored with the vault.</param>
    /// <param name="iterations">Argon2id passes (t), from <see cref="MinIterations"/> to <see cref="MaxIterations"/>.</param>
    /// <param name="memoryKiB">Argon2id memory size in KiB (m), from <see cref="MinMemoryKiB"/> to <see cref="MaxMemoryKiB"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="masterPassword"/> is null.</exception>
    /// <exception cref="ArgumentException">The salt is not exactly 16 bytes long.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A cost parameter is outside its allowed range.</exception>
    public static byte[] DeriveMasterKey(
        string masterPassword,
        ReadOnlySpan<byte> salt,
        int iterations = DefaultIterations,
        int memoryKiB = DefaultMemoryKiB)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);

        if (salt.Length != SaltSize)
        {
            throw new ArgumentException($"The salt must be {SaltSize} bytes long.", nameof(salt));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, MinIterations);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(iterations, MaxIterations);
        ArgumentOutOfRangeException.ThrowIfLessThan(memoryKiB, MinMemoryKiB);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(memoryKiB, MaxMemoryKiB);

        var parameters = new Argon2Parameters
        {
            DegreeOfParallelism = 1,
            MemorySize = memoryKiB,
            NumberOfPasses = iterations,
        };
        var argon2id = PasswordBasedKeyDerivationAlgorithm.Argon2id(in parameters);

        // Pinned so the GC cannot copy these bytes before they are wiped. The strings cannot be cleared (spec §11).
        var normalized = masterPassword.Normalize(NormalizationForm.FormKC);
        var passwordBytes = GC.AllocateArray<byte>(Encoding.UTF8.GetByteCount(normalized), pinned: true);
        Encoding.UTF8.GetBytes(normalized, passwordBytes);
        try
        {
            return argon2id.DeriveBytes(passwordBytes, salt, KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
