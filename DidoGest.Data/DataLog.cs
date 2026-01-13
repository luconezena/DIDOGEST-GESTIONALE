using System;
using System.IO;

namespace DidoGest.Data;

internal static class DataLog
{
    public static void Error(string source, Exception ex)
    {
        try
        {
            var logsDir = AppPaths.LogsDirectory;
            Directory.CreateDirectory(logsDir);

            var file = Path.Combine(logsDir, $"data-errors-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:O}] {source}\n{ex}\n\n");
        }
        catch
        {
            // best effort
        }
    }
}
