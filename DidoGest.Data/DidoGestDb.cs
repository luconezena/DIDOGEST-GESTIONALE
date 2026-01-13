using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data;

public static class DidoGestDb
{
    private const string ProviderSqlite = "Sqlite";
    private const string ProviderSqlServer = "SqlServer";

    public const string DefaultDbFileName = "DidoGest.db";
    public const string DefaultConnectionString = "Data Source=DidoGest.db";

    private sealed class SettingsDto
    {
        public string? PercorsoDatabase { get; set; }
        public string? DatabaseProvider { get; set; }
        public string? SqlServerConnectionString { get; set; }

        public string? SqlServerHost { get; set; }
        public string? SqlServerInstance { get; set; }
        public int? SqlServerPort { get; set; }
        public string? SqlServerDatabase { get; set; }
        public string? SqlServerAuthMode { get; set; }
        public string? SqlServerUserId { get; set; }
        public string? SqlServerPassword { get; set; }
    }

    public static string GetDatabaseProvider()
    {
        var dto = LoadSettings();
        var raw = (dto?.DatabaseProvider ?? string.Empty).Trim();
        if (string.Equals(raw, ProviderSqlServer, StringComparison.OrdinalIgnoreCase))
            return ProviderSqlServer;
        return ProviderSqlite;
    }

    public static string GetConnectionString()
    {
        var dto = LoadSettings();
        var provider = GetDatabaseProvider();

        if (string.Equals(provider, ProviderSqlServer, StringComparison.OrdinalIgnoreCase))
        {
            var cs = (dto?.SqlServerConnectionString ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cs))
                cs = BuildSqlServerConnectionStringFromSettings(dto);
            if (string.IsNullOrWhiteSpace(cs))
                throw new InvalidOperationException("Provider database impostato su SQL Server, ma manca la configurazione (stringa di connessione o campi guidati) in DidoGest.settings.json");
            return cs;
        }

        var dbPath = GetDatabasePathFromSettings(dto);
        return string.IsNullOrWhiteSpace(dbPath) ? DefaultConnectionString : $"Data Source={dbPath}";
    }

    private static string BuildSqlServerConnectionStringFromSettings(SettingsDto? dto)
    {
        if (dto == null) return string.Empty;

        var host = (dto.SqlServerHost ?? string.Empty).Trim();
        var instance = (dto.SqlServerInstance ?? string.Empty).Trim();
        var database = (dto.SqlServerDatabase ?? string.Empty).Trim();
        var auth = (dto.SqlServerAuthMode ?? string.Empty).Trim();
        var user = (dto.SqlServerUserId ?? string.Empty).Trim();
        var pwd = dto.SqlServerPassword ?? string.Empty;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database))
            return string.Empty;

        var server = host;
        if (!string.IsNullOrWhiteSpace(instance))
            server = host + "\\" + instance;
        if (dto.SqlServerPort.HasValue && dto.SqlServerPort.Value > 0)
            server = server + "," + dto.SqlServerPort.Value;

        // Default: per workgroup usiamo SQL auth.
        var useWindowsAuth = string.Equals(auth, "Windows", StringComparison.OrdinalIgnoreCase);
        if (!useWindowsAuth && (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pwd)))
            return string.Empty;

        // Encrypt=False per evitare problemi di certificati in LAN; si può rendere configurabile in futuro.
        var cs = $"Server={server};Database={database};TrustServerCertificate=True;Encrypt=False;";
        if (useWindowsAuth)
            cs += "Trusted_Connection=True;";
        else
            cs += $"User Id={user};Password={pwd};";

        return cs;
    }

    public static string GetDatabasePath()
    {
        if (string.Equals(GetDatabaseProvider(), ProviderSqlServer, StringComparison.OrdinalIgnoreCase))
            return GetSafeDatabaseIdentifier();

        return GetDatabasePathFromSettings(LoadSettings())
               ?? AppPaths.DefaultDatabasePath;
    }

    public static string GetSafeDatabaseIdentifier()
    {
        try
        {
            if (string.Equals(GetDatabaseProvider(), ProviderSqlServer, StringComparison.OrdinalIgnoreCase))
            {
                var dto = LoadSettings();
                var cs = (dto?.SqlServerConnectionString ?? string.Empty).Trim();
                var builder = new DbConnectionStringBuilder { ConnectionString = cs };

                string? server = TryGet(builder, "Server") ?? TryGet(builder, "Data Source") ?? TryGet(builder, "Address") ?? TryGet(builder, "Network Address");
                string? database = TryGet(builder, "Database") ?? TryGet(builder, "Initial Catalog");

                server = string.IsNullOrWhiteSpace(server) ? "(server)" : server;
                database = string.IsNullOrWhiteSpace(database) ? "(db)" : database;
                return $"SQL Server: {server} / {database}";
            }
        }
        catch (Exception ex)
        {
            DataLog.Error("DidoGestDb.GetSafeDatabaseIdentifier", ex);
        }

        // fallback sqlite
        return GetDatabasePathFromSettings(LoadSettings())
               ?? Path.Combine(AppContext.BaseDirectory, DefaultDbFileName);
    }

    public static DidoGestDbContext CreateContext()
    {
        var provider = GetDatabaseProvider();
        var connectionString = GetConnectionString();

        var builder = new DbContextOptionsBuilder<DidoGestDbContext>();
        if (string.Equals(provider, ProviderSqlServer, StringComparison.OrdinalIgnoreCase))
        {
            builder.UseSqlServer(connectionString);
        }
        else
        {
            builder.UseSqlite(connectionString);
        }

        return new DidoGestDbContext(builder.Options);
    }

    private static SettingsDto? LoadSettings()
    {
        try
        {
            var settingsPath = AppPaths.SettingsPath;
            if (!File.Exists(settingsPath))
                return null;

            var json = File.ReadAllText(settingsPath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<SettingsDto>(json);
        }
        catch (Exception ex)
        {
            DataLog.Error("DidoGestDb.LoadSettings", ex);
            return null;
        }
    }

    private static string? TryGet(DbConnectionStringBuilder builder, string key)
    {
        if (builder.TryGetValue(key, out var v) && v != null)
            return v.ToString();
        return null;
    }

    private static string? GetDatabasePathFromSettings(SettingsDto? dto)
    {
        try
        {
            var raw = (dto?.PercorsoDatabase ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            // Se l'utente indica una cartella, usiamo il nome di default.
            if (Directory.Exists(raw))
                raw = Path.Combine(raw, DefaultDbFileName);

            // Se è un path relativo, lo rendiamo assoluto rispetto alla cartella dell'eseguibile.
            var fullPath = Path.IsPathRooted(raw)
                ? raw
                : Path.Combine(AppPaths.BaseDirectory, raw);

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            return fullPath;
        }
        catch (Exception ex)
        {
            DataLog.Error("DidoGestDb.GetDatabasePathFromSettings", ex);
            return null;
        }
    }
}
