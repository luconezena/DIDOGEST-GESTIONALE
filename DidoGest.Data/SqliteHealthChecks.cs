using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace DidoGest.Data;

public enum SqliteStartupIssue
{
    None = 0,
    DirectoryNotWritable = 1,
    DatabaseReadOnly = 2,
    DatabaseLocked = 3,
    DatabaseAccessError = 4,
}

public sealed record SqliteHealthCheckResult(
    SqliteStartupIssue Issue,
    string DatabasePath,
    string DatabaseDirectory,
    string Details)
{
    public bool Ok => Issue == SqliteStartupIssue.None;

    public static SqliteHealthCheckResult Success(string databasePath, string databaseDirectory) =>
        new(SqliteStartupIssue.None, databasePath, databaseDirectory, string.Empty);
}

public static class SqliteHealthChecks
{
    public static SqliteHealthCheckResult CheckCurrentDatabase(bool includeLockCheck)
    {
        var provider = DidoGestDb.GetDatabaseProvider();
        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            return SqliteHealthCheckResult.Success("(SQL Server)", string.Empty);

        var dbPath = DidoGestDb.GetDatabasePath();
        var cs = DidoGestDb.GetConnectionString();
        return CheckSqliteDatabase(dbPath, cs, includeLockCheck);
    }

    public static SqliteHealthCheckResult CheckSqliteDatabase(string dbPath, string sqliteConnectionString, bool includeLockCheck)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (string.IsNullOrWhiteSpace(dir))
            dir = AppPaths.BaseDirectory;

        // 1) Directory writable probe
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".didogest-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "test");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            return new SqliteHealthCheckResult(
                SqliteStartupIssue.DirectoryNotWritable,
                dbPath,
                dir,
                ex.Message);
        }

        // 2) Read-only attribute
        try
        {
            if (File.Exists(dbPath))
            {
                var attrs = File.GetAttributes(dbPath);
                if (attrs.HasFlag(FileAttributes.ReadOnly))
                {
                    return new SqliteHealthCheckResult(
                        SqliteStartupIssue.DatabaseReadOnly,
                        dbPath,
                        dir,
                        "Il file del database è impostato come sola lettura.");
                }
            }
        }
        catch (Exception ex)
        {
            // Best-effort: l'access/open check (step 3) gestisce i casi reali.
            // Qui logghiamo solo per diagnosi su installazioni con ACL/FS strane.
            DataLog.Error($"SqliteHealthChecks.CheckSqliteDatabase.GetAttributes: {dbPath}", ex);
        }

        // 3) File open read/write (ACL, read-only FS, etc)
        try
        {
            if (File.Exists(dbPath))
            {
                using var _ = File.Open(dbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
        }
        catch (Exception ex)
        {
            return new SqliteHealthCheckResult(
                SqliteStartupIssue.DatabaseAccessError,
                dbPath,
                dir,
                ex.Message);
        }

        if (!includeLockCheck)
            return SqliteHealthCheckResult.Success(dbPath, dir);

        // 4) Locked/busy check: BEGIN IMMEDIATE
        try
        {
            var builder = new SqliteConnectionStringBuilder(sqliteConnectionString)
            {
                Mode = SqliteOpenMode.ReadWriteCreate,
                DefaultTimeout = 1
            };

            using var con = new SqliteConnection(builder.ToString());
            con.Open();

            using var cmd = con.CreateCommand();
            cmd.CommandText = "BEGIN IMMEDIATE; ROLLBACK;";
            cmd.ExecuteNonQuery();

            return SqliteHealthCheckResult.Success(dbPath, dir);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6)
        {
            return new SqliteHealthCheckResult(
                SqliteStartupIssue.DatabaseLocked,
                dbPath,
                dir,
                ex.Message);
        }
        catch (Exception ex)
        {
            return new SqliteHealthCheckResult(
                SqliteStartupIssue.DatabaseAccessError,
                dbPath,
                dir,
                ex.Message);
        }
    }
}
