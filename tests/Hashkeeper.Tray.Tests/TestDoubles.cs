using Hashkeeper.Tray.Services;
using Hashkeeper.Tray.Services.Vault;

namespace Hashkeeper.Tray.Tests;

internal sealed class FakeShutdownService : IShutdownService
{
    public int ShutdownCalls { get; private set; }

    public void Shutdown() => ShutdownCalls++;
}

internal sealed class FakeVaultWindowService : IVaultWindowService
{
    public int ShowCalls { get; private set; }

    public void ShowCreateOrUnlock() => ShowCalls++;
}

/// <summary>A vault session without crypto: the password is compared as plain text.</summary>
internal sealed class FakeVaultSession : IVaultSession
{
    private string? _password;

    public event EventHandler? LockStateChanged;

    public bool VaultExists => _password is not null;

    public bool IsUnlocked { get; private set; }

    /// <summary>When set, CreateVault and TryUnlock throw it, e.g. to simulate a disk error.</summary>
    public Exception? FailWith { get; set; }

    public List<string> UnlockAttempts { get; } = [];

    public static FakeVaultSession WithVault(string password) => new() { _password = password };

    public void CreateVault(string masterPassword)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        _password = masterPassword;
        SetUnlocked(true);
    }

    public bool TryUnlock(string masterPassword)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        UnlockAttempts.Add(masterPassword);
        if (masterPassword != _password)
        {
            return false;
        }

        SetUnlocked(true);
        return true;
    }

    public void Lock() => SetUnlocked(false);

    public T WithVaultKey<T>(VaultKeyFunc<T> action) => action(new byte[32]);

    private void SetUnlocked(bool value)
    {
        if (IsUnlocked == value)
        {
            return;
        }

        IsUnlocked = value;
        LockStateChanged?.Invoke(this, EventArgs.Empty);
    }
}
