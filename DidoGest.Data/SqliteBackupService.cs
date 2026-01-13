using Microsoft.Data.Sqlite;
using System.Globalization;

namespace DidoGest.Data;

public static class SqliteBackupService
{
    private const int DefaultKeepLast = 30;

    public static void CreateBackup(string sourceDbPath, string destinationDbPath)
    {
        if (string.IsNullOrWhiteSpace(sourceDbPath))
            throw new ArgumentException("Percorso DB sorgente non valido.", nameof(sourceDbPath));

        if (string.IsNullOrWhiteSpace(destinationDbPath))
            throw new ArgumentException("Percorso DB destinazione non valido.", nameof(destinationDbPath));

        if (!File.Exists(sourceDbPath))
            throw new FileNotFoundException("Database sorgente non trovato.", sourceDbPath);

        var destDir = Path.GetDirectoryName(destinationDbPath);
        if (!string.IsNullOrWhiteSpace(destDir))
            Directory.CreateDirectory(destDir);

        // Se esiste già, rimuoviamo per evitare casi strani (file corrotto/lock) sul database di destinazione.
        if (File.Exists(destinationDbPath))
            File.Delete(destinationDbPath);

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourceDbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        };
        using var source = new SqliteConnection(sourceBuilder.ToString());
        source.Open();

        var destBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationDbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        using var destination = new SqliteConnection(destBuilder.ToString());
        destination.Open();

        // Snapshot consistente tramite sqlite3_backup (sicuro anche con DB in uso).
        source.BackupDatabase(destination);

        // Best-effort: assicurati che il file esista ed abbia dimensione plausibile.
        var fi = new FileInfo(destinationDbPath);
        if (!fi.Exists || fi.Length == 0)
            throw new InvalidOperationException("Backup creato, ma il file risultante non è valido (vuoto o mancante).");
    }

    public static void PruneOldBackups(string backupDirectory, int keepLast = DefaultKeepLast)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            return;

        if (keepLast <= 0)
            return;

        if (!Directory.Exists(backupDirectory))
            return;

        // Pattern coerente con MainWindow: DidoGest_yyyyMMdd_HHmmss.db
        // Ordina preferibilmente per timestamp nel nome (più affidabile di CreationTime), fallback su LastWriteTimeUtc.
        var files = Directory.GetFiles(backupDirectory, "DidoGest_*.db", SearchOption.TopDirectoryOnly)
            .Select(p => new FileInfo(p))
            .Select(f => new
            {
                File = f,
                Stamp = TryParseBackupTimestampUtc(f.Name) ?? f.LastWriteTimeUtc
            })
            .OrderByDescending(x => x.Stamp)
            .Select(x => x.File)
            .ToList();

        if (files.Count <= keepLast)
            return;

        foreach (var file in files.Skip(keepLast))
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                DataLog.Error($"SqliteBackupService.PruneOldBackups.Delete: {file.FullName}", ex);
            }
        }
    }

    private static DateTime? TryParseBackupTimestampUtc(string fileName)
    {
        // atteso: DidoGest_yyyyMMdd_HHmmss.db
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        const string prefix = "DidoGest_";
        const string suffix = ".db";

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return null;

        var core = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
        if (DateTime.TryParseExact(core, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt.ToUniversalTime();

        return null;
    }
}
