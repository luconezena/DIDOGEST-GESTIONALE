using System;
using System.IO;

namespace DidoGest.Data;

/// <summary>
/// Centralizza i percorsi usati dall'app.
///
/// Nota: per la versione portatile i percorsi restano nella cartella dell'eseguibile.
/// In futuro (versione installabile) sarà sufficiente modificare qui i default
/// per puntare a una cartella utente (es. %LOCALAPPDATA%).
/// </summary>
public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;

    public static string LogsDirectory => Path.Combine(BaseDirectory, "Logs");

    public static string SettingsPath => Path.Combine(BaseDirectory, "DidoGest.settings.json");

    public static string DefaultDatabasePath => Path.Combine(BaseDirectory, DidoGestDb.DefaultDbFileName);
}
