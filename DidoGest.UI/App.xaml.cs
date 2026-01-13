using System;
using System.Linq;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using DidoGest.Data;
using DidoGest.Core.Entities;
using DidoGest.UI.Services;
using DidoGest.UI.Windows;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Evita shutdown impliciti prima di avere una MainWindow.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Forza cultura italiana per formattazioni (valuta in Euro, date, separatori decimali)
        // così l'app non dipende dalle impostazioni regionali del PC.
        var culture = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        Environment.CurrentDirectory = AppContext.BaseDirectory;

        Directory.CreateDirectory(AppPaths.LogsDirectory);

        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Crash("DispatcherUnhandledException", args.Exception);
            MessageBox.Show(
                "Errore non gestito. Dettagli salvati in Logs.\n\n" + args.Exception.Message,
                "DIDO-GEST - Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLog.Crash("AppDomain.UnhandledException", ex);
            }
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Crash("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);

        // Check operativo: su installazioni portable è facile finire in cartelle non scrivibili (es. Program Files).
        // Se il DB SQLite non può scrivere nella sua cartella, meglio fermarsi subito con un messaggio chiaro.
        if (!StartupChecks.EnsureSqliteDatabaseDirectoryWritableOrShowError())
        {
            Shutdown();
            return;
        }

        // Check operativo: database SQLite bloccato (altra istanza/processo).
        if (!StartupChecks.EnsureSqliteDatabaseNotLockedOrShowError())
        {
            Shutdown();
            return;
        }

        // Inizializzazione database
        using (var dbContext = DidoGestDb.CreateContext())
        {
            dbContext.Database.EnsureCreated();

            var settings = new AppSettingsService().Load();

            // Micro-migrazioni SQLite per DB esistenti (schema evolution senza Migrations EF)
            try
            {
                var provider = DidoGestDb.GetDatabaseProvider();
                if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                    SqliteSchemaMigrator.EnsureSchema(dbContext);
                else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
                    SqlServerSchemaMigrator.EnsureSchema(dbContext);
            }
            catch (Exception ex)
            {
                var dbPath = DidoGestDb.GetSafeDatabaseIdentifier();
                MessageBox.Show(
                    "Il database esistente richiede un aggiornamento schema (migrazione).\n\n" +
                    $"Dettagli: {ex.Message}\n\n" +
                    $"Suggerimento: fai una copia del database ({dbPath}) e riavvia. Se l'errore persiste, potrebbe essere necessario ricreare il DB.",
                    "DIDO-GEST - Errore database",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown();
                return;
            }

            // Hardening coerenza dati (idempotente): DB vecchi possono avere Pagato=true senza DataPagamento.
            try
            {
                DataHardeningService.EnsureDataPagamentoForFatturePagate(dbContext);
            }
            catch
            {
                // best-effort: non bloccare l'avvio
            }

            // Diagnostica integrità: duplicati su (TipoDocumento, NumeroDocumento) impediscono indice UNIQUE.
            // Best-effort: logga in Logs senza bloccare l'avvio.
            try
            {
                var provider = DidoGestDb.GetDatabaseProvider();
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open) conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT TipoDocumento, NumeroDocumento, COUNT(*) AS Cnt " +
                    "FROM Documenti " +
                    "WHERE TipoDocumento IS NOT NULL AND NumeroDocumento IS NOT NULL " +
                    "GROUP BY TipoDocumento, NumeroDocumento " +
                    "HAVING COUNT(*) > 1 " +
                    "ORDER BY COUNT(*) DESC;";

                using var reader = cmd.ExecuteReader();
                var any = false;
                var lines = new System.Collections.Generic.List<string>();
                var take = 0;
                while (reader.Read())
                {
                    any = true;
                    if (take++ >= 50) break;
                    var tipo = reader["TipoDocumento"]?.ToString() ?? string.Empty;
                    var num = reader["NumeroDocumento"]?.ToString() ?? string.Empty;
                    var cntObj = reader["Cnt"];
                    var cnt = cntObj == null || cntObj == DBNull.Value ? 0 : Convert.ToInt32(cntObj);
                    lines.Add($"- {tipo} / {num}  (x{cnt})");
                }

                if (any)
                {
                    AppLog.DbIntegrity(
                        $"[{DateTime.Now:O}] Duplicati rilevati su (TipoDocumento, NumeroDocumento). Provider={provider}\n" +
                        "Questo impedisce l'indice UNIQUE; l'app userà solo un indice non-univoco finché i duplicati non vengono rimossi.\n" +
                        "Esempi (max 50):\n" + string.Join("\n", lines) + "\n\n");
                }
            }
            catch
            {
                // best-effort
            }

            // DEMO (idempotente): abilitabile/disabilitabile da Impostazioni.
            if (settings.EnableDemoData != false)
            {
                try
                {
                    DemoDataSeedService.EnsureDemoData(dbContext);
                }
                catch
                {
                    // best-effort: la DEMO non deve bloccare l'avvio
                }
            }

            // Bootstrap utenti iniziali (solo se tabella vuota)
            try
            {
                new AuthService().EnsureDefaultUsers(dbContext);
            }
            catch
            {
                // best-effort: non bloccare avvio (es. DB in sola lettura)
            }
        }

        // Login obbligatorio
        var login = new LoginWindow();
        var ok = login.ShowDialog() == true && login.LoggedIn && UserSession.CurrentUser != null;
        if (!ok)
        {
            Shutdown();
            return;
        }

        // Avvio MainWindow
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

}
