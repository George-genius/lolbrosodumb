using CodeShellDesktopHost.Models;
using Microsoft.Data.Sqlite;

namespace CodeShellDesktopHost.Services;

public sealed class SqliteStorageService
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteStorageService(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        _connectionString = builder.ToString();
    }

    public async Task InitializeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode = WAL;

                CREATE TABLE IF NOT EXISTS KvStore (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS BlobStore (
                    Key TEXT PRIMARY KEY,
                    FileName TEXT NULL,
                    MimeType TEXT NOT NULL,
                    Data BLOB NOT NULL,
                    UpdatedUtc TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS AppSettings (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    UpdatedUtc TEXT NOT NULL
                );
                """;
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetItemAsync(string key)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Value FROM KvStore WHERE Key = $key LIMIT 1;";
            cmd.Parameters.AddWithValue("$key", key);
            return await cmd.ExecuteScalarAsync() as string;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> SetItemAsync(string key, string value)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO KvStore (Key, Value, UpdatedUtc)
                VALUES ($key, $value, $updatedUtc)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedUtc = excluded.UpdatedUtc;
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> RemoveItemAsync(string key)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM KvStore WHERE Key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> ClearKvAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM KvStore;";
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<List<string>> GetKeysAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Key FROM KvStore ORDER BY Key;";
            var keys = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) keys.Add(reader.GetString(0));
            return keys;
        }
        finally { _gate.Release(); }
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = $key LIMIT 1;";
            cmd.Parameters.AddWithValue("$key", key);
            return await cmd.ExecuteScalarAsync() as string;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> SetSettingAsync(string key, string value)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO AppSettings (Key, Value, UpdatedUtc)
                VALUES ($key, $value, $updatedUtc)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedUtc = excluded.UpdatedUtc;
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> SetSettingIfMissingAsync(string key, string value)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedUtc) VALUES ($key, $value, $updatedUtc);";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        finally { _gate.Release(); }
    }

    public async Task PutBlobAsync(string key, string? fileName, string mimeType, byte[] data)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO BlobStore (Key, FileName, MimeType, Data, UpdatedUtc)
                VALUES ($key, $fileName, $mimeType, $data, $updatedUtc)
                ON CONFLICT(Key) DO UPDATE SET FileName = excluded.FileName, MimeType = excluded.MimeType, Data = excluded.Data, UpdatedUtc = excluded.UpdatedUtc;
                """;
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$fileName", (object?)fileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$mimeType", mimeType);
            cmd.Parameters.AddWithValue("$data", data);
            cmd.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _gate.Release(); }
    }

    public async Task<BlobItem?> GetBlobAsync(string key)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Key, FileName, MimeType, Data, UpdatedUtc FROM BlobStore WHERE Key = $key LIMIT 1;";
            cmd.Parameters.AddWithValue("$key", key);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;
            return new BlobItem
            {
                Key = reader.GetString(0),
                FileName = reader.IsDBNull(1) ? null : reader.GetString(1),
                MimeType = reader.GetString(2),
                Data = (byte[])reader[3],
                UpdatedUtc = reader.GetString(4)
            };
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> RemoveBlobAsync(string key)
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "DELETE FROM BlobStore WHERE Key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        finally { _gate.Release(); }
    }

    public async Task<List<object>> ListBlobsAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await using var con = new SqliteConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT Key, FileName, MimeType, length(Data), UpdatedUtc FROM BlobStore ORDER BY UpdatedUtc DESC, Key ASC;";
            var items = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new
                {
                    key = reader.GetString(0),
                    fileName = reader.IsDBNull(1) ? null : reader.GetString(1),
                    mimeType = reader.GetString(2),
                    size = reader.GetInt64(3),
                    updatedUtc = reader.GetString(4)
                });
            }
            return items;
        }
        finally { _gate.Release(); }
    }
}
