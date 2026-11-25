using System.IO;
using Microsoft.Data.Sqlite;

namespace Hashkeeper.Tray.Services.Storage;

/// <summary>
/// SQLite implementation of <see cref="ILocalVaultStore"/>: one database file per vault.
/// </summary>
/// <remarks>
/// The file and its schema are created on first use, not in the constructor, so resolving the
/// store from DI never touches the disk. Each call opens its own connection; the vault is small
/// and used by one process, so connection pooling is disabled to release the file right away.
/// </remarks>
public sealed class LocalVaultStore : ILocalVaultStore
{
    /// <summary>Schema version stored in <c>PRAGMA user_version</c>.</summary>
    internal const int SchemaVersion = 1;

    private const string CreateSchemaSql = """
        CREATE TABLE IF NOT EXISTS vault_metadata (
            id                INTEGER PRIMARY KEY CHECK (id = 1),
            kdf               TEXT    NOT NULL,
            kdf_iterations    INTEGER NOT NULL,
            kdf_memory_kib    INTEGER NOT NULL,
            kdf_parallelism   INTEGER NOT NULL,
            kdf_salt          BLOB    NOT NULL,
            wrapped_vault_key BLOB    NOT NULL
        );

        CREATE TABLE IF NOT EXISTS entries (
            id             TEXT    PRIMARY KEY,
            version        INTEGER NOT NULL,
            updated_at_ms  INTEGER NOT NULL,
            is_deleted     INTEGER NOT NULL,
            encrypted_data BLOB    NOT NULL
        );
        """;

    private readonly string _connectionString;
    private readonly Lock _initLock = new();
    private bool _initialized;

    public LocalVaultStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FilePath = Path.GetFullPath(filePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();
    }

    /// <summary>Default vault file: <c>%LOCALAPPDATA%\Hashkeeper\vault.db</c>.</summary>
    /// <remarks>Local, not roaming, AppData: the vault belongs to this device; sync is a separate opt-in.</remarks>
    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Hashkeeper",
        "vault.db");

    public string FilePath { get; }

    public bool VaultExists() => File.Exists(FilePath) && LoadMetadata() is not null;

    public VaultMetadata? LoadMetadata()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kdf, kdf_iterations, kdf_memory_kib, kdf_parallelism, kdf_salt, wrapped_vault_key
            FROM vault_metadata
            WHERE id = 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new VaultMetadata(
            Kdf: reader.GetString(0),
            KdfIterations: reader.GetInt32(1),
            KdfMemoryKiB: reader.GetInt32(2),
            KdfParallelism: reader.GetInt32(3),
            KdfSalt: (byte[])reader[4],
            WrappedVaultKey: (byte[])reader[5]);
    }

    public void SaveMetadata(VaultMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Kdf, nameof(metadata));
        ArgumentNullException.ThrowIfNull(metadata.KdfSalt, nameof(metadata));
        ArgumentNullException.ThrowIfNull(metadata.WrappedVaultKey, nameof(metadata));

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO vault_metadata
                (id, kdf, kdf_iterations, kdf_memory_kib, kdf_parallelism, kdf_salt, wrapped_vault_key)
            VALUES
                (1, $kdf, $iterations, $memory, $parallelism, $salt, $wrappedVaultKey);
            """;
        command.Parameters.AddWithValue("$kdf", metadata.Kdf);
        command.Parameters.AddWithValue("$iterations", metadata.KdfIterations);
        command.Parameters.AddWithValue("$memory", metadata.KdfMemoryKiB);
        command.Parameters.AddWithValue("$parallelism", metadata.KdfParallelism);
        command.Parameters.AddWithValue("$salt", metadata.KdfSalt);
        command.Parameters.AddWithValue("$wrappedVaultKey", metadata.WrappedVaultKey);
        command.ExecuteNonQuery();
    }

    public StoredEntry? LoadEntry(Guid id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, version, updated_at_ms, is_deleted, encrypted_data
            FROM entries
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", FormatId(id));

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadEntry(reader) : null;
    }

    public IReadOnlyList<StoredEntry> LoadEntries(bool includeDeleted = false)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, version, updated_at_ms, is_deleted, encrypted_data
            FROM entries
            WHERE $includeDeleted = 1 OR is_deleted = 0
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("$includeDeleted", includeDeleted ? 1 : 0);

        var entries = new List<StoredEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public void SaveEntry(StoredEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.EncryptedData, nameof(entry));
        if (entry.Id == Guid.Empty)
        {
            throw new ArgumentException("The entry ID must not be empty.", nameof(entry));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(entry.Version, nameof(entry));

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO entries (id, version, updated_at_ms, is_deleted, encrypted_data)
            VALUES ($id, $version, $updatedAt, $isDeleted, $encryptedData);
            """;
        command.Parameters.AddWithValue("$id", FormatId(entry.Id));
        command.Parameters.AddWithValue("$version", entry.Version);
        command.Parameters.AddWithValue("$updatedAt", entry.UpdatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$isDeleted", entry.IsDeleted ? 1 : 0);
        command.Parameters.AddWithValue("$encryptedData", entry.EncryptedData);
        command.ExecuteNonQuery();
    }

    // Same canonical form as the associated data in docs/crypto.md §7.2: lowercase, with hyphens.
    private static string FormatId(Guid id) => id.ToString("D");

    private static StoredEntry ReadEntry(SqliteDataReader reader) => new(
        Id: Guid.ParseExact(reader.GetString(0), "D"),
        Version: reader.GetInt64(1),
        UpdatedAt: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(2)),
        IsDeleted: reader.GetInt64(3) != 0,
        EncryptedData: (byte[])reader[4]);

    private SqliteConnection Open()
    {
        EnsureInitialized();

        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureInitialized()
    {
        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var fileVersion = ReadSchemaVersion(connection);
            if (fileVersion > SchemaVersion)
            {
                throw new InvalidOperationException(
                    $"The vault file was created by a newer version of Hashkeeper (schema {fileVersion}, supported {SchemaVersion}).");
            }

            using var transaction = connection.BeginTransaction();
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = $"{CreateSchemaSql}\nPRAGMA user_version = {SchemaVersion};";
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            _initialized = true;
        }
    }

    private static long ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)command.ExecuteScalar()!;
    }
}
