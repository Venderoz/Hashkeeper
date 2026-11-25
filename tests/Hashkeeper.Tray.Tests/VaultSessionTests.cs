using System.IO;
using Hashkeeper.Crypto;
using Hashkeeper.Tray.Services.Storage;
using Hashkeeper.Tray.Services.Vault;

namespace Hashkeeper.Tray.Tests;

public class VaultSessionTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public void NoVault_IsLockedAndDoesNotExist()
    {
        var session = new VaultSession(new InMemoryVaultStore());

        Assert.False(session.VaultExists);
        Assert.False(session.IsUnlocked);
    }

    [Fact]
    public void CreateVault_SavesSpecDefaultsAndLeavesVaultUnlocked()
    {
        var store = new InMemoryVaultStore();
        var session = new VaultSession(store);

        session.CreateVault(Password);

        var metadata = store.LoadMetadata();
        Assert.NotNull(metadata);
        Assert.Equal("argon2id", metadata.Kdf);
        Assert.Equal(Argon2idMasterKeyDeriver.DefaultIterations, metadata.KdfIterations);
        Assert.Equal(Argon2idMasterKeyDeriver.DefaultMemoryKiB, metadata.KdfMemoryKiB);
        Assert.Equal(1, metadata.KdfParallelism);
        Assert.Equal(Argon2idMasterKeyDeriver.SaltSize, metadata.KdfSalt.Length);
        Assert.Equal(73, metadata.WrappedVaultKey.Length);
        Assert.True(session.VaultExists);
        Assert.True(session.IsUnlocked);
    }

    [Fact]
    public void CreateVault_WhenVaultExists_Throws()
    {
        var session = new VaultSession(new InMemoryVaultStore());
        session.CreateVault(Password);

        Assert.Throws<InvalidOperationException>(() => session.CreateVault(Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("elevenchars")]
    public void CreateVault_ShortPassword_Throws(string password)
    {
        var store = new InMemoryVaultStore();
        var session = new VaultSession(store);

        Assert.Throws<ArgumentException>(() => session.CreateVault(password));
        Assert.Null(store.LoadMetadata());
    }

    [Fact]
    public void CreateVault_MinimumLengthPassword_IsAccepted()
    {
        var session = new VaultSession(new InMemoryVaultStore());

        session.CreateVault(new string('x', IVaultSession.MinimumPasswordLength));

        Assert.True(session.IsUnlocked);
    }

    [Fact]
    public void TryUnlock_CorrectPassword_RestoresTheSameVaultKey()
    {
        var store = new InMemoryVaultStore();
        var session = new VaultSession(store);
        session.CreateVault(Password);
        var keyAtCreation = session.WithVaultKey(key => key.ToArray());
        session.Lock();

        // A fresh session, as after restarting the app.
        var restarted = new VaultSession(store);
        Assert.True(restarted.TryUnlock(Password));

        Assert.True(restarted.IsUnlocked);
        Assert.Equal(keyAtCreation, restarted.WithVaultKey(key => key.ToArray()));
    }

    [Theory]
    [InlineData("correct horse battery stapl")]
    [InlineData("Correct horse battery staple")]
    [InlineData(" correct horse battery staple")]
    [InlineData("")]
    public void TryUnlock_WrongPassword_ReturnsFalseAndStaysLocked(string wrongPassword)
    {
        var store = NewLockedVault();
        var session = new VaultSession(store);

        Assert.False(session.TryUnlock(wrongPassword));
        Assert.False(session.IsUnlocked);
    }

    [Fact]
    public void TryUnlock_NoVault_Throws()
    {
        var session = new VaultSession(new InMemoryVaultStore());

        Assert.Throws<InvalidOperationException>(() => session.TryUnlock(Password));
    }

    [Fact]
    public void TryUnlock_TamperedWrappedKey_ReturnsFalse()
    {
        var store = NewLockedVault();
        var metadata = store.LoadMetadata()!;
        var tampered = metadata.WrappedVaultKey.ToArray();
        tampered[^1] ^= 0x01;
        store.SaveMetadata(metadata with { WrappedVaultKey = tampered });

        Assert.False(new VaultSession(store).TryUnlock(Password));
    }

    [Theory]
    [InlineData("scrypt", 3, 65536, 1)]          // unknown KDF
    [InlineData("argon2id", 3, 65536, 4)]        // unsupported parallelism
    [InlineData("argon2id", 1, 65536, 1)]        // iterations below the minimum (downgrade)
    [InlineData("argon2id", 3, 1024, 1)]         // memory below the minimum (downgrade)
    [InlineData("argon2id", 1000, 65536, 1)]     // iterations above the maximum (DoS)
    [InlineData("argon2id", 3, int.MaxValue, 1)] // memory above the maximum (DoS)
    public void TryUnlock_InvalidKdfParameters_ReturnsFalse(string kdf, int iterations, int memoryKiB, int parallelism)
    {
        var store = NewLockedVault();
        var metadata = store.LoadMetadata()!;
        store.SaveMetadata(metadata with
        {
            Kdf = kdf,
            KdfIterations = iterations,
            KdfMemoryKiB = memoryKiB,
            KdfParallelism = parallelism,
        });

        Assert.False(new VaultSession(store).TryUnlock(Password));
    }

    [Fact]
    public void TryUnlock_InvalidSalt_ReturnsFalse()
    {
        var store = NewLockedVault();
        store.SaveMetadata(store.LoadMetadata()! with { KdfSalt = new byte[8] });

        Assert.False(new VaultSession(store).TryUnlock(Password));
    }

    [Fact]
    public void Lock_ClearsAccessToTheVaultKey()
    {
        var session = new VaultSession(new InMemoryVaultStore());
        session.CreateVault(Password);

        session.Lock();

        Assert.False(session.IsUnlocked);
        Assert.Throws<InvalidOperationException>(() => session.WithVaultKey(key => key.Length));
    }

    [Fact]
    public void LockStateChanged_IsRaisedOnUnlockAndLockOnly()
    {
        var store = NewLockedVault();
        var session = new VaultSession(store);
        var raised = 0;
        session.LockStateChanged += (_, _) => raised++;

        session.Lock();                      // already locked: no event
        Assert.False(session.TryUnlock("wrong password!"));
        Assert.True(session.TryUnlock(Password));
        session.Lock();

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Dispose_LocksTheVault()
    {
        var session = new VaultSession(new InMemoryVaultStore());
        session.CreateVault(Password);

        session.Dispose();

        Assert.False(session.IsUnlocked);
    }

    [Fact]
    public void VaultKey_EncryptsEntriesThatSurviveARestart()
    {
        var store = new InMemoryVaultStore();
        var session = new VaultSession(store);
        session.CreateVault(Password);
        var entryId = Guid.NewGuid().ToString("D");
        var secret = "{\"name\":\"Netflix\"}"u8.ToArray();
        var encrypted = session.WithVaultKey(key => CredentialEncryptor.Encrypt(secret, key, entryId));
        session.Lock();

        var restarted = new VaultSession(store);
        Assert.True(restarted.TryUnlock(Password));

        Assert.Equal(secret, restarted.WithVaultKey(key => CredentialEncryptor.Decrypt(encrypted, key, entryId)));
    }

    [Fact]
    public void WorksWithTheSqliteStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hashkeeper-tests", Guid.NewGuid().ToString("N"));
        var filePath = Path.Combine(directory, "vault.db");
        try
        {
            new VaultSession(new LocalVaultStore(filePath)).CreateVault(Password);

            var restarted = new VaultSession(new LocalVaultStore(filePath));

            Assert.True(restarted.VaultExists);
            Assert.False(restarted.TryUnlock("not the password"));
            Assert.True(restarted.TryUnlock(Password));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static InMemoryVaultStore NewLockedVault()
    {
        var store = new InMemoryVaultStore();
        new VaultSession(store).CreateVault(Password);
        return store;
    }

    private sealed class InMemoryVaultStore : ILocalVaultStore
    {
        private readonly Dictionary<Guid, StoredEntry> _entries = [];
        private VaultMetadata? _metadata;

        public string FilePath => ":memory:";

        public bool VaultExists() => _metadata is not null;

        public VaultMetadata? LoadMetadata() => _metadata;

        public void SaveMetadata(VaultMetadata metadata) => _metadata = metadata;

        public StoredEntry? LoadEntry(Guid id) => _entries.GetValueOrDefault(id);

        public IReadOnlyList<StoredEntry> LoadEntries(bool includeDeleted = false) =>
            [.. _entries.Values.Where(e => includeDeleted || !e.IsDeleted).OrderBy(e => e.Id.ToString("D"))];

        public void SaveEntry(StoredEntry entry) => _entries[entry.Id] = entry;
    }
}
