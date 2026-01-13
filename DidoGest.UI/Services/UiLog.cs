using System;
using System.IO;
using DidoGest.Data;

namespace DidoGest.UI.Services;

public static class UiLog
{
    public static void Error(string source, Exception ex)
    {
        try
        {
            var logsDir = AppPaths.LogsDirectory;
            Directory.CreateDirectory(logsDir);

            var file = Path.Combine(logsDir, $"ui-errors-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:O}] {source}\n{ex}\n\n");
        }
        catch
        {
            // best effort
        }
    }
}
