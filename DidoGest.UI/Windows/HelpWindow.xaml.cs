using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using DidoGest.UI.Services;

namespace DidoGest.UI.Windows;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        Loaded += HelpWindow_Loaded;
    }

    private void HelpWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Nota: in publish single-file self-contained, AppContext.BaseDirectory puo' puntare
            // alla cartella di estrazione temporanea. Quindi proviamo prima la cartella dell'exe.
            var candidates = BuildHelpCandidatePaths();
            var found = candidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(found))
            {
                TxtHelp.Text = File.ReadAllText(found, Encoding.UTF8);
                return;
            }

            TxtHelp.Text = "Guida non trovata (HELP.md).\n\n" +
                          "Verifica che il file sia presente nella cartella del programma.\n\n" +
                          "Percorsi provati:\n- " + string.Join("\n- ", candidates);
        }
        catch (Exception ex)
        {
            UiLog.Error("HelpWindow.Load", ex);
            TxtHelp.Text = "Errore caricamento guida:\n\n" + ex.Message;
        }
    }

    private static List<string> BuildHelpCandidatePaths()
    {
        var paths = new List<string>();

        try
        {
            // .NET 6+: percorso reale del processo (funziona bene anche in single-file)
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                var dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    paths.Add(Path.Combine(dir, "HELP.md"));
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            var mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(mainModulePath))
            {
                var dir = Path.GetDirectoryName(mainModulePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    paths.Add(Path.Combine(dir, "HELP.md"));
            }
        }
        catch
        {
            // ignore
        }

        // Fallback: base directory (puo' essere temp in single-file)
        paths.Add(Path.Combine(AppContext.BaseDirectory, "HELP.md"));

        // Fallback: current directory (se avviato da shell o link)
        try
        {
            paths.Add(Path.Combine(Directory.GetCurrentDirectory(), "HELP.md"));
        }
        catch
        {
            // ignore
        }

        // Dedup e ripulisci
        return paths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void BtnChiudi_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
