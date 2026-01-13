using System;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Services;
using DidoGest.UI.Windows;
using Microsoft.Win32;

namespace DidoGest.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "DIDO-GEST - Gestionale Professionale v1.0";
        
        // Carica la dashboard iniziale
        LoadDashboard();

        ApplyFeatureFlags();
    }

    private void ApplyFeatureFlags()
    {
        try
        {
            var settings = new AppSettingsService().Load();
            var feEnabled = settings.EnableFatturazioneElettronica == true;

            if (MenuFatturazioneElettronicaRoot != null)
                MenuFatturazioneElettronicaRoot.Visibility = feEnabled ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            // best effort: se non riusciamo a leggere settings, non blocchiamo l'app
        }
    }

    private void RefreshCurrentContent()
    {
        if (ContentArea.Content is not UserControl current)
            return;

        var type = current.GetType();
        try
        {
            if (Activator.CreateInstance(type) is UserControl refreshed)
            {
                ContentArea.Content = refreshed;
            }
        }
        catch
        {
            // best effort: se una view non ha ctor parameterless o fallisce, non blocchiamo l'utente
        }
    }

    private void Navigate(Func<object> factory)
    {
        try
        {
            ContentArea.Content = factory();
        }
        catch (Exception ex)
        {
            UiLog.Error("MainWindow.Navigate", ex);

            MessageBox.Show(
                "Errore apertura sezione:\n\n" + ex.Message,
                "DIDO-GEST - Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadDashboard()
    {
        ContentArea.Content = new TextBlock
        {
            Text = "Benvenuto in DIDO-GEST\n\nSeleziona un modulo dal menu per iniziare.",
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
    }

    // Event handlers per menu
    private void MenuAnagraficheClienti_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Anagrafiche.ClientiView());
    }

    private void MenuAnagraficheFornitori_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Anagrafiche.FornitoriView());
    }

    private void MenuAnagraficheAgenti_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Anagrafiche.AgentiView());
    }

    private void MenuMagazzinoArticoli_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Magazzino.ArticoliView());
    }

    private void MenuMagazzinoGiacenze_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Magazzino.GiacenzeView());
    }

    private void MenuMagazzinoSottoscorta_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Magazzino.SottoscortaView());
    }

    private void MenuMagazzinoMovimenti_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Magazzino.MovimentiView());
    }

    private void MenuMagazzinoListini_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Magazzino.ListiniView());
    }

    private void MenuDocumentiDDT_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Documenti.DDTView());
    }

    private void MenuDocumentiFatture_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Documenti.FattureView());
    }

    private void MenuDocumentiFatturaAccompagnatoria_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var db = DidoGestDb.CreateContext();
            var w = new Windows.DocumentoEditWindow(db, "FATTURA_ACCOMPAGNATORIA");
            w.Owner = this;
            w.ShowDialog();

            RefreshCurrentContent();
        }
        catch (Exception ex)
        {
            UiLog.Error("MainWindow.MenuDocumentiFatturaAccompagnatoria_Click", ex);
            MessageBox.Show(
                "Errore apertura finestra documento:\n\n" + ex.Message,
                "DIDO-GEST - Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    
    private void MenuAiutoGuida_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var w = new HelpWindow
            {
                Owner = this
            };
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            UiLog.Error("MainWindow.MenuAiutoGuida_Click", ex);
            MessageBox.Show(
                "Errore apertura guida:\n\n" + ex.Message,
                "DIDO-GEST - Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MenuDocumentiPreventivi_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Documenti.PreventiviView());
    }

    private void MenuDocumentiOrdini_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Documenti.OrdiniView());
    }

    private void MenuFatturazioneElettronica_Click(object sender, RoutedEventArgs e)
    {
        // Rileggiamo le impostazioni al click (l'utente può averle appena cambiate)
        try
        {
            var settings = new AppSettingsService().Load();
            if (settings.EnableFatturazioneElettronica != true)
            {
                MessageBox.Show(
                    "Fatturazione elettronica disabilitata.\n\nVai in Utility → Impostazioni e abilita 'Fatturazione elettronica'.",
                    "DIDO-GEST",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                ApplyFeatureFlags();
                return;
            }
        }
        catch
        {
            // se la lettura settings fallisce, proseguiamo comunque con la view
        }

        Navigate(() => new Views.FatturazioneElettronica.FatturazioneElettronicaView());
    }

    private void MenuContabilitaPrimaNota_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Contabilita.PrimaNotaView());
    }

    private void MenuContabilitaRegistriIVA_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Contabilita.RegistriIVAView());
    }

    private void MenuContabilitaMastrini_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Contabilita.MastriniView());
    }

    private void MenuContabilitaScadenzarioIncassi_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Contabilita.ScadenzarioIncassiView());
    }

    private void MenuAssistenzeSchede_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Assistenza.SchedeAssistenzaView());
    }

    private void MenuAssistenzeContratti_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Assistenza.ContrattiView());
    }

    private void MenuAssistenzeCantieri_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Cantieri.CantieriView());
    }

    private void MenuArchivio_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Archivio.ArchivioView());
    }

    private void MenuUtilityImpostazioni_Click(object sender, RoutedEventArgs e)
    {
        Navigate(() => new Views.Utility.ImpostazioniView());
    }

    private void MenuUtilityGestioneUtenti_Click(object sender, RoutedEventArgs e)
    {
        var u = UserSession.CurrentUser;
        var isAllowed = u != null &&
                        (string.Equals(u.Ruolo, "ADMIN", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(u.Ruolo, "SUPERADMIN", StringComparison.OrdinalIgnoreCase));

        if (!isAllowed)
        {
            MessageBox.Show(
                "Permesso negato.\n\nSolo ADMIN o SUPERADMIN possono gestire gli utenti.",
                "DIDO-GEST",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var w = new UserManagementWindow
        {
            Owner = this
        };
        w.ShowDialog();
    }

    private void MenuUtilityBackup_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(DidoGestDb.GetDatabaseProvider(), "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "Backup database\n\n" +
                "Il database è configurato su SQL Server.\n" +
                "Il backup va eseguito con gli strumenti di SQL Server (backup .bak).",
                "Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var sourceDb = DidoGestDb.GetDatabasePath();
        var backupDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Backup");
        var backupFile = System.IO.Path.Combine(backupDir, $"DidoGest_{DateTime.Now:yyyyMMdd_HHmmss}.db");

        var result = MessageBox.Show(
            "Backup Database\n\n" +
            "Vuoi creare un backup del database?\n\n" +
            $"File: {sourceDb}\n" +
            $"Destinazione: {backupFile}",
            "Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                SqliteBackupService.CreateBackup(sourceDb, backupFile);

                try
                {
                    SqliteBackupService.PruneOldBackups(backupDir, keepLast: 30);
                }
                catch (Exception ex)
                {
                    UiLog.Error("MainWindow.MenuUtilityBackup_Click.PruneOldBackups", ex);
                }
                
                MessageBox.Show($"Backup completato con successo!\n\nFile: {backupFile}", "Successo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                UiLog.Error("MainWindow.MenuUtilityBackup_Click", ex);
                MessageBox.Show($"Errore durante il backup:\n{ex.Message}", "Errore",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuUtilityPulisciDemo_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Pulisci dati DEMO\n\n" +
            "Questa operazione elimina dal database i record di esempio (DEMO).\n" +
            "Non verranno toccati i tuoi dati reali.\n\n" +
            "Vuoi continuare?",
            "DIDO-GEST - Pulisci DEMO",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            using var dbContext = DidoGestDb.CreateContext();

            var purge = DemoDataPurgeService.PurgeDemoData(dbContext);

            // In ogni caso, se l'utente ha scelto "Pulisci DEMO", disabilita il reseed automatico.
            var settingsSvc = new AppSettingsService();
            var settings = settingsSvc.Load();
            settings.EnableDemoData = false;
            settingsSvc.Save(settings);

            if (purge.Totale == 0)
            {
                MessageBox.Show(
                    "Nessun dato DEMO trovato da eliminare.\n\n" +
                    "La DEMO è stata disattivata e non verrà ricreata automaticamente.\n" +
                    "(Puoi riattivarla da Utility → Impostazioni)",
                    "DIDO-GEST",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                StatusMessage.Text = "DEMO disattivata";
                return;
            }

            MessageBox.Show(
                "Pulizia DEMO completata.\n\n" +
                $"Clienti: {purge.Clienti}\n" +
                $"Fornitori: {purge.Fornitori}\n" +
                $"Articoli: {purge.Articoli}\n" +
                $"Contratti: {purge.Contratti}\n" +
                $"Schede assistenza: {purge.SchedeAssistenza}\n" +
                $"Interventi assistenza: {purge.AssistenzaInterventi}\n" +
                $"Ordini: {purge.Ordini}\n" +
                $"Righe ordine: {purge.RigheOrdine}\n" +
                $"Documenti: {purge.Documenti}\n" +
                $"Righe documento: {purge.RigheDocumento}\n" +
                $"Movimenti magazzino: {purge.MovimentiMagazzino}\n" +
                $"Giacenze: {purge.GiacenzeMagazzino}\n\n" +
                "La DEMO è stata disattivata e non verrà ricreata automaticamente.\n" +
                "(Puoi riattivarla da Utility → Impostazioni)",
                "DIDO-GEST",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            StatusMessage.Text = "Dati DEMO rimossi";
            RefreshCurrentContent();
        }
        catch (Exception ex)
        {
            UiLog.Error("MainWindow.MenuUtilityPulisciDemo_Click", ex);
            MessageBox.Show(
                "Errore durante la pulizia dei dati DEMO:\n\n" + ex.Message,
                "DIDO-GEST - Errore",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void MenuUtilityImpostaLogoStampa_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Seleziona logo per stampa",
                Filter = "Immagini|*.png;*.jpg;*.jpeg;*.bmp"
            };

            if (dlg.ShowDialog(this) != true)
                return;

            var ext = System.IO.Path.GetExtension(dlg.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";

            var assetsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets");
            System.IO.Directory.CreateDirectory(assetsDir);
            var destPath = System.IO.Path.Combine(assetsDir, "LogoStampa" + ext);
            System.IO.File.Copy(dlg.FileName, destPath, true);

            var svc = new AppSettingsService();
            var s = svc.Load();
            s.LogoStampaPath = destPath;
            svc.Save(s);

            MessageBox.Show(
                "Logo impostato.\n\n" +
                "Nella stampa verrà incluso automaticamente (se presente).",
                "DIDO-GEST",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("MainWindow.MenuUtilityImpostaLogoStampa_Click", ex);
            MessageBox.Show($"Errore impostazione logo: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuUtilityRimuoviLogoStampa_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var svc = new AppSettingsService();
            var s = svc.Load();
            s.LogoStampaPath = null;
            svc.Save(s);

            MessageBox.Show(
                "Logo rimosso.\n\nLa stampa verrà generata senza logo.",
                "DIDO-GEST",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("MainWindow.MenuUtilityRimuoviLogoStampa_Click", ex);
            MessageBox.Show($"Errore rimozione logo: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuUtilityInfo_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "DIDO-GEST - Gestionale Professionale\nVersione 1.0\n\n" +
            "© 2025 DIDO Software\n\n" +
            "Gestionale completo per:\n" +
            "- Magazzino e Fatturazione\n" +
            "- Fatturazione Elettronica\n" +
            "- Contabilità\n" +
            "- Assistenze e Contratti\n" +
            "- Archiviazione Documentale",
            "Informazioni",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void MenuEsci_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Sei sicuro di voler uscire?", "Conferma", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }
}
