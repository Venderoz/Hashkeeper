using System.Security.Cryptography;
using Hashkeeper.Crypto;
using Hashkeeper.Tray.Services.Storage;

namespace Hashkeeper.Tray.Services.Vault;

/// <summary>
/// Default <see cref="IVaultSession"/>: ties the crypto building blocks (Hashkeeper.Crypto) to the
/// local vault file (<see cref="ILocalVaultStore"/>).
/// </summary>
/// <remarks>
/// The Master Key and Wrap Key exist only inside <see cref="CreateVault"/> and <see cref="TryUnlock"/>
/// and are cleared before they return. The Vault Key is kept in a pinned array, so the garbage
/// collector cannot leave copies of it behind, and is cleared on <see cref="Lock"/> and
/// <see cref="Dispose"/>.
/// </remarks>
public sealed class VaultSession(ILocalVaultStore store) : IVaultSession, IDisposable
{
    internal const string KdfName = "argon2id";

    private const int VaultKeySize = 32;

    private readonly Lock _sync = new();
    private byte[]? _vaultKey;

    public event EventHandler? LockStateChanged;

    public bool VaultExists => store.VaultExists();

    public bool IsUnlocked
    {
        get
        {
            lock (_sync)
            {
                return _vaultKey is not null;
            }
        }
    }

    public void CreateVault(string masterPassword)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);
        if (masterPassword.Length < IVaultSession.MinimumPasswordLength)
        {
            throw new ArgumentException(
                $"The Master Password must be at least {IVaultSession.MinimumPasswordLength} characters long.",
                nameof(masterPassword));
        }

        if (store.VaultExists())
        {
            throw new InvalidOperationException("A vault already exists.");
        }

        var salt = Argon2idMasterKeyDeriver.GenerateSalt();

        // Same as VaultKeyWrapper.GenerateVaultKey, but written straight into a pinned buffer.
        var vaultKey = NewPinnedKey();
        RandomNumberGenerator.Fill(vaultKey);

        byte[]? masterKey = null;
        byte[]? wrapKey = null;
        try
        {
            masterKey = Argon2idMasterKeyDeriver.DeriveMasterKey(masterPassword, salt);
            wrapKey = HkdfSubkeyDeriver.DeriveWrapKey(masterKey);
            var wrappedVaultKey = VaultKeyWrapper.Wrap(vaultKey, wrapKey);

            store.SaveMetadata(new VaultMetadata(
                KdfName,
                Argon2idMasterKeyDeriver.DefaultIterations,
                Argon2idMasterKeyDeriver.DefaultMemoryKiB,
                KdfParallelism: 1,
                salt,
                wrappedVaultKey));
        }
        catch
        {
            CryptographicOperations.ZeroMemory(vaultKey);
            throw;
        }
        finally
        {
            ZeroIfNotNull(masterKey);
            ZeroIfNotNull(wrapKey);
        }

        SetVaultKey(vaultKey);
    }

    public bool TryUnlock(string masterPassword)
    {
        ArgumentNullException.ThrowIfNull(masterPassword);

        var metadata = store.LoadMetadata()
            ?? throw new InvalidOperationException("No vault exists yet.");

        // Unknown KDF or unsupported parameters count as corrupted data: same answer as a wrong password.
        if (metadata.Kdf != KdfName || metadata.KdfParallelism != 1)
        {
            return false;
        }

        byte[]? masterKey = null;
        byte[]? wrapKey = null;
        byte[] unwrapped;
        try
        {
            masterKey = Argon2idMasterKeyDeriver.DeriveMasterKey(
                masterPassword, metadata.KdfSalt, metadata.KdfIterations, metadata.KdfMemoryKiB);
            wrapKey = HkdfSubkeyDeriver.DeriveWrapKey(masterKey);
            unwrapped = VaultKeyWrapper.Unwrap(metadata.WrappedVaultKey, wrapKey);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            // Wrong password, tampered data, or stored KDF parameters out of range (downgrade or DoS attempt).
            return false;
        }
        finally
        {
            ZeroIfNotNull(masterKey);
            ZeroIfNotNull(wrapKey);
        }

        var vaultKey = NewPinnedKey();
        unwrapped.AsSpan().CopyTo(vaultKey);
        CryptographicOperations.ZeroMemory(unwrapped);

        SetVaultKey(vaultKey);
        return true;
    }

    public void Lock()
    {
        bool wasUnlocked;
        lock (_sync)
        {
            wasUnlocked = _vaultKey is not null;
            ZeroIfNotNull(_vaultKey);
            _vaultKey = null;
        }

        if (wasUnlocked)
        {
            LockStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public T WithVaultKey<T>(VaultKeyFunc<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_sync)
        {
            if (_vaultKey is null)
            {
                throw new InvalidOperationException("The vault is locked.");
            }

            return action(_vaultKey);
        }
    }

    public void Dispose() => Lock();

    private void SetVaultKey(byte[] vaultKey)
    {
        lock (_sync)
        {
            ZeroIfNotNull(_vaultKey);
            _vaultKey = vaultKey;
        }

        LockStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static byte[] NewPinnedKey() => GC.AllocateArray<byte>(VaultKeySize, pinned: true);

    private static void ZeroIfNotNull(byte[]? buffer)
    {
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
