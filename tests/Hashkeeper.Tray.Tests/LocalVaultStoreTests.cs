using System.IO;
using System.Security.Cryptography;
using Hashkeeper.Crypto;
using Hashkeeper.Tray.Services.Storage;
using Microsoft.Data.Sqlite;

namespace Hashkeeper.Tray.Tests;

public sealed class LocalVaultStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _filePath;

    public LocalVaultStoreTests()
    {
        // A missing subfolder checks that the store creates the directory itself.
        _directory = Path.Combine(Path.GetTempPath(), "hashkeeper-tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_directory, "vault", "vault.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void NewStore_DoesNotTouchTheDisk()
    {
        var store = new LocalVaultStore(_filePath);

        Assert.False(store.VaultExists());
        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public void EmptyVault_HasNoMetadataAndNoEntries()
    {
        var store = new LocalVaultStore(_filePath);

        Assert.Null(store.LoadMetadata());
        Assert.Empty(store.LoadEntries(includeDeleted: true));
        Assert.Null(store.LoadEntry(Guid.NewGuid()));
        Assert.True(File.Exists(_filePath));
        Assert.False(store.VaultExists());
    }

    [Fact]
    public void SaveMetadata_ThenLoad_ReturnsSameValues()
    {
        var store = new LocalVaultStore(_filePath);
        var metadata = NewMetadata();

        store.SaveMetadata(metadata);
        var loaded = store.LoadMetadata();

        AssertMetadataEqual(metadata, loaded);
        Assert.True(store.VaultExists());
    }

    [Fact]
    public void SaveMetadata_Twice_ReplacesTheFirst()
    {
        var store = new LocalVaultStore(_filePath);
        store.SaveMetadata(NewMetadata());
        var replacement = NewMetadata() with { KdfIterations = 4 };

        store.SaveMetadata(replacement);

        AssertMetadataEqual(replacement, store.LoadMetadata());
    }

    [Fact]
    public void SaveEntry_ThenLoad_ReturnsSameValues()
    {
        var store = new LocalVaultStore(_filePath);
        var entry = NewEntry();

        store.SaveEntry(entry);

        AssertEntryEqual(entry, store.LoadEntry(entry.Id));
    }

    [Fact]
    public void SaveEntry_SameId_ReplacesTheEntry()
    {
        var store = new LocalVaultStore(_filePath);
        var entry = NewEntry();
        store.SaveEntry(entry);
        var updated = entry with { Version = 2, EncryptedData = RandomNumberGenerator.GetBytes(60) };

        store.SaveEntry(updated);

        AssertEntryEqual(updated, store.LoadEntry(entry.Id));
        Assert.Single(store.LoadEntries());
    }

    [Fact]
    public void LoadEntries_SkipsTombstonesUnlessAsked()
    {
        var store = new LocalVaultStore(_filePath);
        var live = NewEntry();
        var deleted = NewEntry() with { IsDeleted = true, EncryptedData = [] };
        store.SaveEntry(live);
        store.SaveEntry(deleted);

        Assert.Equal([live.Id], store.LoadEntries().Select(e => e.Id));
        Assert.Equal(2, store.LoadEntries(includeDeleted: true).Count);
        Assert.True(store.LoadEntry(deleted.Id)!.IsDeleted);
    }

    [Fact]
    public void LoadEntries_AreOrderedById()
    {
        var store = new LocalVaultStore(_filePath);
        var entries = Enumerable.Range(0, 5).Select(_ => NewEntry()).ToList();
        foreach (var entry in entries)
        {
            store.SaveEntry(entry);
        }

        var expected = entries.Select(e => e.Id.ToString("D")).Order(StringComparer.Ordinal);

        Assert.Equal(expected, store.LoadEntries().Select(e => e.Id.ToString("D")));
    }

    [Fact]
    public void UpdatedAt_IsStoredInUtcWithMillisecondPrecision()
    {
        var store = new LocalVaultStore(_filePath);
        var localTime = new DateTimeOffset(2026, 11, 25, 14, 30, 15, 123, TimeSpan.FromHours(1)).AddTicks(4567);
        var entry = NewEntry() with { UpdatedAt = localTime };

        store.SaveEntry(entry);
        var loaded = store.LoadEntry(entry.Id)!.UpdatedAt;

        Assert.Equal(TimeSpan.Zero, loaded.Offset);
        Assert.Equal(localTime.ToUnixTimeMilliseconds(), loaded.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Data_SurvivesANewStoreInstance()
    {
        var metadata = NewMetadata();
        var entry = NewEntry();
        var first = new LocalVaultStore(_filePath);
        first.SaveMetadata(metadata);
        first.SaveEntry(entry);

        var second = new LocalVaultStore(_filePath);

        AssertMetadataEqual(metadata, second.LoadMetadata());
        AssertEntryEqual(entry, second.LoadEntry(entry.Id));
    }

    [Fact]
    public void Store_ReleasesTheFile()
    {
        var store = new LocalVaultStore(_filePath);
        store.SaveEntry(NewEntry());

        // Would throw if a connection kept the file open.
        File.Delete(_filePath);

        Assert.False(File.Exists(_filePath));
    }

    [Fact]
    public void Store_SetsTheSchemaVersion()
    {
        new LocalVaultStore(_filePath).SaveEntry(NewEntry());

        Assert.Equal(LocalVaultStore.SchemaVersion, ReadUserVersion());
    }

    [Fact]
    public void Store_RejectsFileFromNewerSchema()
    {
        new LocalVaultStore(_filePath).SaveEntry(NewEntry());
        SetUserVersion(LocalVaultStore.SchemaVersion + 1);

        var store = new LocalVaultStore(_filePath);

        Assert.Throws<InvalidOperationException>(() => store.LoadEntries());
    }

    [Fact]
    public void SaveEntry_InvalidEntry_Throws()
    {
        var store = new LocalVaultStore(_filePath);

        Assert.Throws<ArgumentException>(() => store.SaveEntry(NewEntry() with { Id = Guid.Empty }));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.SaveEntry(NewEntry() with { Version = -1 }));
        Assert.Throws<ArgumentNullException>(() => store.SaveEntry(NewEntry() with { EncryptedData = null! }));
    }

    [Fact]
    public void DefaultFilePath_IsInLocalAppData()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hashkeeper", "vault.db");

        Assert.Equal(expected, LocalVaultStore.DefaultFilePath);
    }

    // End to end with the real crypto: create a vault, store an entry, reopen the file and read it back.
    [Fact]
    public void RealVault_RoundTripsThroughCryptoAndStorage()
    {
        const string password = "correct horse battery staple";
        var entryId = Guid.NewGuid();
        var secret = "{\"name\":\"Netflix\",\"password\":\"s3cret\"}"u8.ToArray();

        var salt = Argon2idMasterKeyDeriver.GenerateSalt();
        var masterKey = Argon2idMasterKeyDeriver.DeriveMasterKey(password, salt);
        var vaultKey = VaultKeyWrapper.GenerateVaultKey();
        var store = new LocalVaultStore(_filePath);
        store.SaveMetadata(new VaultMetadata(
            "argon2id",
            Argon2idMasterKeyDeriver.DefaultIterations,
            Argon2idMasterKeyDeriver.DefaultMemoryKiB,
            1,
            salt,
            VaultKeyWrapper.Wrap(vaultKey, HkdfSubkeyDeriver.DeriveWrapKey(masterKey))));
        store.SaveEntry(new StoredEntry(
            entryId, 1, DateTimeOffset.UtcNow, false, CredentialEncryptor.Encrypt(secret, vaultKey, entryId.ToString("D"))));

        var reopened = new LocalVaultStore(_filePath);
        var metadata = reopened.LoadMetadata()!;
        var unlockKey = Argon2idMasterKeyDeriver.DeriveMasterKey(
            password, metadata.KdfSalt, metadata.KdfIterations, metadata.KdfMemoryKiB);
        var unwrapped = VaultKeyWrapper.Unwrap(metadata.WrappedVaultKey, HkdfSubkeyDeriver.DeriveWrapKey(unlockKey));
        var decrypted = CredentialEncryptor.Decrypt(
            reopened.LoadEntry(entryId)!.EncryptedData, unwrapped, entryId.ToString("D"));

        Assert.Equal(secret, decrypted);
    }

    private static VaultMetadata NewMetadata() => new(
        "argon2id", 3, 65536, 1, RandomNumberGenerator.GetBytes(16), RandomNumberGenerator.GetBytes(73));

    private static StoredEntry NewEntry() => new(
        Guid.NewGuid(), 1, DateTimeOffset.UtcNow, false, RandomNumberGenerator.GetBytes(57));

    private static void AssertMetadataEqual(VaultMetadata expected, VaultMetadata? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Kdf, actual.Kdf);
        Assert.Equal(expected.KdfIterations, actual.KdfIterations);
        Assert.Equal(expected.KdfMemoryKiB, actual.KdfMemoryKiB);
        Assert.Equal(expected.KdfParallelism, actual.KdfParallelism);
        Assert.Equal(expected.KdfSalt, actual.KdfSalt);
        Assert.Equal(expected.WrappedVaultKey, actual.WrappedVaultKey);
    }

    private static void AssertEntryEqual(StoredEntry expected, StoredEntry? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.UpdatedAt.ToUnixTimeMilliseconds(), actual.UpdatedAt.ToUnixTimeMilliseconds());
        Assert.Equal(expected.IsDeleted, actual.IsDeleted);
        Assert.Equal(expected.EncryptedData, actual.EncryptedData);
    }

    private long ReadUserVersion()
    {
        using var connection = OpenRaw();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }

    private void SetUserVersion(int version)
    {
        using var connection = OpenRaw();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenRaw()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _filePath,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }
}
