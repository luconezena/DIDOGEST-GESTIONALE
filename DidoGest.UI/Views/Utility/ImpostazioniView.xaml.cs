using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace DidoGest.UI.Views.Utility;

public partial class ImpostazioniView : UserControl
{
    private readonly AppSettingsService _service;
    private AppSettings _settings = new();

    private const string FeModeCommercialista = "Commercialista";
    private const string FeModeServer = "Server";

    public ImpostazioniView()
    {
        InitializeComponent();
        _service = new AppSettingsService();
        Loaded += ImpostazioniView_Loaded;

        CmbDbProvider.SelectionChanged += (_, _) => UpdateDbUiState();
        CmbSqlAuth.SelectionChanged += (_, _) => UpdateSqlAuthUiState();

        if (ChkFeEnabled != null)
            ChkFeEnabled.Checked += (_, _) => UpdateFeUiState();
        if (ChkFeEnabled != null)
            ChkFeEnabled.Unchecked += (_, _) => UpdateFeUiState();
        if (CmbFeMode != null)
            CmbFeMode.SelectionChanged += (_, _) => UpdateFeUiState();
    }

    private void ImpostazioniView_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = _service.Load();

        CmbDbProvider.ItemsSource = new[] { "Sqlite", "SqlServer" };
        var provider = string.IsNullOrWhiteSpace(_settings.DatabaseProvider) ? "Sqlite" : _settings.DatabaseProvider.Trim();
        CmbDbProvider.SelectedItem = provider;

        CmbSqlAuth.ItemsSource = new[] { "Sql", "Windows" };
        var auth = string.IsNullOrWhiteSpace(_settings.SqlServerAuthMode) ? "Sql" : _settings.SqlServerAuthMode.Trim();
        CmbSqlAuth.SelectedItem = auth;

        TxtRagione.Text = _settings.RagioneSociale ?? string.Empty;
        TxtPiva.Text = _settings.PartitaIva ?? string.Empty;
        TxtCf.Text = _settings.CodiceFiscale ?? string.Empty;
        TxtIndirizzo.Text = _settings.Indirizzo ?? string.Empty;
        TxtCap.Text = _settings.CAP ?? string.Empty;
        TxtCitta.Text = _settings.Citta ?? string.Empty;
        TxtProv.Text = _settings.Provincia ?? string.Empty;
        TxtTel.Text = _settings.Telefono ?? string.Empty;
        TxtEmail.Text = _settings.Email ?? string.Empty;
        TxtPec.Text = _settings.PEC ?? string.Empty;
        TxtSdi.Text = _settings.CodiceSDI ?? string.Empty;
        TxtDb.Text = _settings.PercorsoDatabase ?? string.Empty;
        TxtArchivio.Text = _settings.PercorsoArchivio ?? string.Empty;

        TxtSqlServerConn.Text = _settings.SqlServerConnectionString ?? string.Empty;

        TxtSqlHost.Text = _settings.SqlServerHost ?? string.Empty;
        TxtSqlInstance.Text = _settings.SqlServerInstance ?? string.Empty;
        TxtSqlPort.Text = _settings.SqlServerPort?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        TxtSqlDatabase.Text = _settings.SqlServerDatabase ?? string.Empty;
        TxtSqlUser.Text = _settings.SqlServerUserId ?? string.Empty;
        PwdSql.Password = _settings.SqlServerPassword ?? string.Empty;

        ChkDemo.IsChecked = _settings.EnableDemoData != false;

        TxtLogo.Text = _settings.LogoStampaPath ?? string.Empty;

        TxtIban.Text = _settings.IBAN ?? string.Empty;
        TxtBancaAzienda.Text = _settings.Banca ?? string.Empty;

        ChkFeEnabled.IsChecked = _settings.EnableFatturazioneElettronica == true;
        CmbFeMode.ItemsSource = new[]
        {
            $"{FeModeCommercialista} (esporta XML)",
            $"{FeModeServer} (invio a server esterno/API)"
        };
        var feModeRaw = string.IsNullOrWhiteSpace(_settings.FeModalitaInvio) ? FeModeCommercialista : _settings.FeModalitaInvio.Trim();
        CmbFeMode.SelectedIndex = string.Equals(feModeRaw, FeModeServer, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        TxtFeCommercialistaFolder.Text = _settings.FeCartellaCommercialista ?? string.Empty;

        TxtFeProvider.Text = _settings.FeProviderNome ?? string.Empty;
        TxtFeApiUrl.Text = _settings.FeApiUrl ?? string.Empty;
        TxtFeApiKey.Text = _settings.FeApiKey ?? string.Empty;

        TxtFirmaProvider.Text = _settings.FirmaProviderNome ?? string.Empty;
        TxtCertPath.Text = _settings.FirmaCertificatoPfxPath ?? string.Empty;
        PwdCert.Password = _settings.FirmaCertificatoPassword ?? string.Empty;

        LblFile.Text = $"File: {_service.FilePath}";

        UpdateDbUiState();
        UpdateSqlAuthUiState();
        UpdateFeUiState();
    }

    private void UpdateFeUiState()
    {
        var enabled = ChkFeEnabled?.IsChecked == true;
        var isServerMode = (CmbFeMode?.SelectedIndex ?? 0) == 1;

        if (CmbFeMode != null)
            CmbFeMode.IsEnabled = enabled;

        // Modalità commercialista
        if (TxtFeCommercialistaFolder != null)
            TxtFeCommercialistaFolder.IsEnabled = enabled && !isServerMode;
        if (BtnSfogliaFeCommercialista != null)
            BtnSfogliaFeCommercialista.IsEnabled = enabled && !isServerMode;

        // Modalità server
        if (TxtFeProvider != null)
            TxtFeProvider.IsEnabled = enabled && isServerMode;
        if (TxtFeApiUrl != null)
            TxtFeApiUrl.IsEnabled = enabled && isServerMode;
        if (TxtFeApiKey != null)
            TxtFeApiKey.IsEnabled = enabled && isServerMode;
    }

    private void BtnSfogliaFeCommercialista_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var current = (TxtFeCommercialistaFolder.Text ?? string.Empty).Trim();

            using var dlg = new Forms.FolderBrowserDialog
            {
                Description = "Seleziona la cartella dove salvare gli XML per il commercialista",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                dlg.SelectedPath = current;

            var result = dlg.ShowDialog();
            if (result == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                TxtFeCommercialistaFolder.Text = dlg.SelectedPath;
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnSfogliaFeCommercialista_Click", ex);
            MessageBox.Show($"Errore selezione cartella: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateDbUiState()
    {
        var provider = (CmbDbProvider.SelectedItem as string ?? "Sqlite").Trim();
        var isSqlServer = string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);

        // SQLite
        TxtDb.IsEnabled = !isSqlServer;
        if (BtnSfogliaDb != null)
            BtnSfogliaDb.IsEnabled = !isSqlServer;

        // SQL Server: abilita campi guidati + conn string
        TxtSqlServerConn.IsEnabled = isSqlServer;
        TxtSqlHost.IsEnabled = isSqlServer;
        TxtSqlInstance.IsEnabled = isSqlServer;
        TxtSqlPort.IsEnabled = isSqlServer;
        TxtSqlDatabase.IsEnabled = isSqlServer;
        CmbSqlAuth.IsEnabled = isSqlServer;
        TxtSqlUser.IsEnabled = isSqlServer;
        PwdSql.IsEnabled = isSqlServer;

        UpdateSqlAuthUiState();
    }

    private void UpdateSqlAuthUiState()
    {
        var provider = (CmbDbProvider.SelectedItem as string ?? "Sqlite").Trim();
        var isSqlServer = string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase);
        if (!isSqlServer)
        {
            TxtSqlUser.IsEnabled = false;
            PwdSql.IsEnabled = false;
            return;
        }

        var auth = (CmbSqlAuth.SelectedItem as string ?? "Sql").Trim();
        var isWindows = string.Equals(auth, "Windows", StringComparison.OrdinalIgnoreCase);
        TxtSqlUser.IsEnabled = !isWindows;
        PwdSql.IsEnabled = !isWindows;
    }

    private void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.RagioneSociale = (TxtRagione.Text ?? string.Empty).Trim();
            _settings.PartitaIva = (TxtPiva.Text ?? string.Empty).Trim();
            _settings.CodiceFiscale = (TxtCf.Text ?? string.Empty).Trim();
            _settings.Indirizzo = (TxtIndirizzo.Text ?? string.Empty).Trim();
            _settings.CAP = (TxtCap.Text ?? string.Empty).Trim();
            _settings.Citta = (TxtCitta.Text ?? string.Empty).Trim();
            _settings.Provincia = (TxtProv.Text ?? string.Empty).Trim();
            _settings.Telefono = (TxtTel.Text ?? string.Empty).Trim();
            _settings.Email = (TxtEmail.Text ?? string.Empty).Trim();
            _settings.PEC = (TxtPec.Text ?? string.Empty).Trim();
            _settings.CodiceSDI = (TxtSdi.Text ?? string.Empty).Trim();
            _settings.PercorsoDatabase = (TxtDb.Text ?? string.Empty).Trim();
            _settings.PercorsoArchivio = (TxtArchivio.Text ?? string.Empty).Trim();

            _settings.DatabaseProvider = (CmbDbProvider.SelectedItem as string ?? "Sqlite").Trim();
            _settings.SqlServerConnectionString = (TxtSqlServerConn.Text ?? string.Empty).Trim();

            _settings.SqlServerHost = (TxtSqlHost.Text ?? string.Empty).Trim();
            _settings.SqlServerInstance = (TxtSqlInstance.Text ?? string.Empty).Trim();
            _settings.SqlServerDatabase = (TxtSqlDatabase.Text ?? string.Empty).Trim();
            _settings.SqlServerAuthMode = (CmbSqlAuth.SelectedItem as string ?? "Sql").Trim();
            _settings.SqlServerUserId = (TxtSqlUser.Text ?? string.Empty).Trim();
            _settings.SqlServerPassword = (PwdSql.Password ?? string.Empty);

            _settings.EnableDemoData = ChkDemo.IsChecked == true;

            var portRaw = (TxtSqlPort.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(portRaw))
            {
                _settings.SqlServerPort = null;
            }
            else if (int.TryParse(portRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port > 0)
            {
                _settings.SqlServerPort = port;
            }
            else
            {
                throw new InvalidOperationException("Porta SQL Server non valida.");
            }

            _settings.LogoStampaPath = (TxtLogo.Text ?? string.Empty).Trim();

            _settings.IBAN = (TxtIban.Text ?? string.Empty).Trim();
            _settings.Banca = (TxtBancaAzienda.Text ?? string.Empty).Trim();

            _settings.EnableFatturazioneElettronica = ChkFeEnabled.IsChecked == true;
            _settings.FeModalitaInvio = (CmbFeMode.SelectedIndex == 1) ? FeModeServer : FeModeCommercialista;
            _settings.FeCartellaCommercialista = (TxtFeCommercialistaFolder.Text ?? string.Empty).Trim();

            _settings.FeProviderNome = (TxtFeProvider.Text ?? string.Empty).Trim();
            _settings.FeApiUrl = (TxtFeApiUrl.Text ?? string.Empty).Trim();
            _settings.FeApiKey = (TxtFeApiKey.Text ?? string.Empty).Trim();

            _settings.FirmaProviderNome = (TxtFirmaProvider.Text ?? string.Empty).Trim();
            _settings.FirmaCertificatoPfxPath = (TxtCertPath.Text ?? string.Empty).Trim();
            _settings.FirmaCertificatoPassword = (PwdCert.Password ?? string.Empty).Trim();

            _service.Save(_settings);
            MessageBox.Show("Impostazioni salvate.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSfogliaDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleziona database (SQLite)",
            Filter = "Database SQLite|*.db;*.sqlite;*.sqlite3|Tutti i file|*.*"
        };

        if (dlg.ShowDialog() == true)
            TxtDb.Text = dlg.FileName;
    }

    private void BtnSqlGenera_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtSqlServerConn.Text = BuildSqlServerConnectionStringFromUi();
            MessageBox.Show("Stringa di connessione generata.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnSqlGenera_Click", ex);
            MessageBox.Show(ex.Message, "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSqlTest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var cs = (TxtSqlServerConn.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cs))
                cs = BuildSqlServerConnectionStringFromUi();

            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<DidoGestDbContext>()
                .UseSqlServer(cs)
                .Options;

            using var ctx = new DidoGestDbContext(options);
            var ok = ctx.Database.CanConnect();

            MessageBox.Show(
                ok ? "Connessione OK." : "Connessione non riuscita.",
                "Test connessione SQL Server",
                MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);

            // Se era vuoto, popoliamo la textbox con quello generato
            if (string.IsNullOrWhiteSpace((TxtSqlServerConn.Text ?? string.Empty).Trim()))
                TxtSqlServerConn.Text = cs;
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnSqlTest_Click", ex);
            MessageBox.Show($"Connessione non riuscita:\n\n{ex.Message}", "Test connessione SQL Server", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string BuildSqlServerConnectionStringFromUi()
    {
        var host = (TxtSqlHost.Text ?? string.Empty).Trim();
        var instance = (TxtSqlInstance.Text ?? string.Empty).Trim();
        var db = (TxtSqlDatabase.Text ?? string.Empty).Trim();
        var auth = (CmbSqlAuth.SelectedItem as string ?? "Sql").Trim();

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Server obbligatorio (nome PC o IP).\nEsempio: PCUFFICIO01");
        if (string.IsNullOrWhiteSpace(db))
            throw new InvalidOperationException("Database obbligatorio.\nEsempio: DidoGest");

        var server = host;
        if (!string.IsNullOrWhiteSpace(instance))
            server = host + "\\" + instance;

        var portRaw = (TxtSqlPort.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(portRaw))
        {
            if (!int.TryParse(portRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port <= 0)
                throw new InvalidOperationException("Porta non valida.");
            server = server + "," + port.ToString(CultureInfo.InvariantCulture);
        }

        var sb = new StringBuilder();
        sb.Append("Server=").Append(server).Append(';');
        sb.Append("Database=").Append(db).Append(';');
        sb.Append("TrustServerCertificate=True;");
        sb.Append("Encrypt=False;");

        if (string.Equals(auth, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("Trusted_Connection=True;");
        }
        else
        {
            var user = (TxtSqlUser.Text ?? string.Empty).Trim();
            var pwd = PwdSql.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(user))
                throw new InvalidOperationException("Nome utente obbligatorio per autenticazione SQL.");
            if (string.IsNullOrWhiteSpace(pwd))
                throw new InvalidOperationException("Password obbligatoria per autenticazione SQL.");

            sb.Append("User Id=").Append(user).Append(';');
            sb.Append("Password=").Append(pwd).Append(';');
        }

        return sb.ToString();
    }

    private void BtnSfogliaArchivio_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Seleziona cartella archivio"
        };

        var res = dlg.ShowDialog();
        if (res == System.Windows.Forms.DialogResult.OK)
            TxtArchivio.Text = dlg.SelectedPath;
    }

    private void BtnSfogliaLogo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleziona logo per stampa",
            Filter = "Immagini|*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dlg.ShowDialog() == true)
            TxtLogo.Text = dlg.FileName;
    }

    private void BtnRimuoviLogo_Click(object sender, RoutedEventArgs e)
    {
        TxtLogo.Text = string.Empty;
    }

    private void BtnExportClienti_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = "Esporta Clienti (CSV)",
                Filter = "CSV|*.csv|Tutti i file|*.*",
                FileName = "clienti.csv"
            };

            if (dlg.ShowDialog() != true)
                return;

            var count = CsvImportExportService.ExportClienti(dlg.FileName);
            MessageBox.Show($"Esportati {count} clienti.", "Esportazione CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnExportClienti_Click", ex);
            MessageBox.Show($"Errore esportazione clienti: {ex.Message}", "Esportazione CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImportClienti_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Importa Clienti (CSV)",
                Filter = "CSV|*.csv|Tutti i file|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            var res = CsvImportExportService.ImportClienti(dlg.FileName);
            MessageBox.Show(
                $"Importazione clienti completata.\n\nInseriti: {res.Inserted}\nAggiornati: {res.Updated}\nSaltati: {res.Skipped}\nErrori: {res.Errors}",
                "Importazione CSV",
                MessageBoxButton.OK,
                res.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnImportClienti_Click", ex);
            MessageBox.Show($"Errore importazione clienti: {ex.Message}", "Importazione CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportFornitori_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = "Esporta Fornitori (CSV)",
                Filter = "CSV|*.csv|Tutti i file|*.*",
                FileName = "fornitori.csv"
            };

            if (dlg.ShowDialog() != true)
                return;

            var count = CsvImportExportService.ExportFornitori(dlg.FileName);
            MessageBox.Show($"Esportati {count} fornitori.", "Esportazione CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnExportFornitori_Click", ex);
            MessageBox.Show($"Errore esportazione fornitori: {ex.Message}", "Esportazione CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImportFornitori_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Importa Fornitori (CSV)",
                Filter = "CSV|*.csv|Tutti i file|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            var res = CsvImportExportService.ImportFornitori(dlg.FileName);
            MessageBox.Show(
                $"Importazione fornitori completata.\n\nInseriti: {res.Inserted}\nAggiornati: {res.Updated}\nSaltati: {res.Skipped}\nErrori: {res.Errors}",
                "Importazione CSV",
                MessageBoxButton.OK,
                res.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnImportFornitori_Click", ex);
            MessageBox.Show($"Errore importazione fornitori: {ex.Message}", "Importazione CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportArticoli_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Title = "Esporta Articoli (CSV)",
                Filter = "CSV|*.csv|Tutti i file|*.*",
                FileName = "articoli.csv"
            };

            if (dlg.ShowDialog() != true)
                return;

            var count = CsvImportExportService.ExportArticoli(dlg.FileName);
            MessageBox.Show($"Esportati {count} articoli.", "Esportazione CSV", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnExportArticoli_Click", ex);
            MessageBox.Show($"Errore esportazione articoli: {ex.Message}", "Esportazione CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImportArticoli_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Title = "Importa Articoli (CSV)",
                Filter = "CSV|*.csv|Tutti i file|*.*"
            };

            if (dlg.ShowDialog() != true)
                return;

            var res = CsvImportExportService.ImportArticoli(dlg.FileName);
            MessageBox.Show(
                $"Importazione articoli completata.\n\nInseriti: {res.Inserted}\nAggiornati: {res.Updated}\nSaltati: {res.Skipped}\nErrori: {res.Errors}",
                "Importazione CSV",
                MessageBoxButton.OK,
                res.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnImportArticoli_Click", ex);
            MessageBox.Show($"Errore importazione articoli: {ex.Message}", "Importazione CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportPackage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Seleziona cartella dove creare il pacchetto migrazione"
            };

            var res = dlg.ShowDialog();
            if (res != System.Windows.Forms.DialogResult.OK)
                return;

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var target = Path.Combine(dlg.SelectedPath, "Migrazione_" + stamp);
            MigrationPackageService.ExportPackage(target);

            MessageBox.Show(
                $"Pacchetto migrazione esportato.\n\nCartella:\n{target}",
                "Esportazione migrazione",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnExportPackage_Click", ex);
            MessageBox.Show($"Errore esportazione pacchetto migrazione: {ex.Message}", "Esportazione migrazione", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImportPackage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Seleziona cartella del pacchetto migrazione (CSV)"
            };

            var resDlg = dlg.ShowDialog();
            if (resDlg != System.Windows.Forms.DialogResult.OK)
                return;

            if (!MigrationPackageService.IsDatabaseProbablyEmpty())
            {
                var confirm = MessageBox.Show(
                    "Il database non sembra vuoto.\n\nImportare un pacchetto migrazione su un DB già popolato può creare duplicati o dati incoerenti.\n\nVuoi continuare comunque?",
                    "Importazione migrazione",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            var res = MigrationPackageService.ImportPackage(dlg.SelectedPath);

            MessageBox.Show(
                $"Import pacchetto migrazione completato.\n\nFile letti: {res.FilesRead}\nInseriti: {res.Inserted}\nAggiornati: {res.Updated}\nSaltati: {res.Skipped}\nErrori: {res.Errors}",
                "Importazione migrazione",
                MessageBoxButton.OK,
                res.Errors == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            UiLog.Error("ImpostazioniView.BtnImportPackage_Click", ex);
            MessageBox.Show($"Errore importazione pacchetto migrazione: {ex.Message}", "Importazione migrazione", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSfogliaCert_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleziona certificato PFX",
            Filter = "Certificato PFX|*.pfx|Tutti i file|*.*"
        };

        if (dlg.ShowDialog() == true)
            TxtCertPath.Text = dlg.FileName;
    }
}
