using System;
using System.IO;
using DidoGest.Data;

namespace DidoGest.UI.Services;

public static class AppLog
{
    public static void Crash(string source, Exception ex)
    {
        try
        {
            var logsDir = AppPaths.LogsDirectory;
            Directory.CreateDirectory(logsDir);

            var file = Path.Combine(logsDir, $"crash-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:O}] {source}\n{ex}\n\n");
        }
        catch
        {
            // best effort
        }
    }

    public static void DbIntegrity(string content)
    {
        try
        {
            var logsDir = AppPaths.LogsDirectory;
            Directory.CreateDirectory(logsDir);

            var file = Path.Combine(logsDir, $"integrita-db-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, content);
        }
        catch
        {
            // best effort
        }
    }
}
