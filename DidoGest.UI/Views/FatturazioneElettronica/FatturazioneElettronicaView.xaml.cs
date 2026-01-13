using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Core.Entities;
using DidoGest.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using DidoGest.UI.Windows;
using DidoGest.UI.Services;

namespace DidoGest.UI.Views.FatturazioneElettronica;

public partial class FatturazioneElettronicaView : UserControl
{
    private readonly DidoGestDbContext _context;
    private readonly AppSettingsService _settingsService = new();

    private const string FeModeCommercialista = "Commercialista";
    private const string FeModeServer = "Server";

    public FatturazioneElettronicaView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        Loaded += async (_, _) => await LoadData();
    }

    private bool IsFatturazioneElettronicaEnabled()
    {
        try
        {
            var s = _settingsService.Load();
            return s.EnableFatturazioneElettronica == true;
        }
        catch
        {
            return true; // best effort
        }
    }

    private (string Mode, AppSettings Settings) GetFeSettings()
    {
        var s = _settingsService.Load();
        var mode = string.IsNullOrWhiteSpace(s.FeModalitaInvio) ? FeModeCommercialista : s.FeModalitaInvio.Trim();
        mode = string.Equals(mode, FeModeServer, StringComparison.OrdinalIgnoreCase) ? FeModeServer : FeModeCommercialista;
        return (mode, s);
    }

    private async Task LoadData()
    {
        if (!IsFatturazioneElettronicaEnabled())
        {
            // Se il modulo è disabilitato, evitiamo query inutili.
            FeDataGrid.ItemsSource = Array.Empty<Documento>();
            return;
        }

        var q = _context.Documenti
            .AsNoTracking()
            .Include(d => d.Cliente)
            .Where(d => d.TipoDocumento == "FATTURA" || d.TipoDocumento == "FATTURA_ACCOMPAGNATORIA")
            .OrderByDescending(d => d.DataDocumento)
            .AsQueryable();

        var search = (SearchTextBox.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(x =>
                (x.NumeroDocumento ?? "").ToLower().Contains(s) ||
                (x.Cliente != null ? (x.Cliente.RagioneSociale ?? "").ToLower() : "").Contains(s) ||
                (x.PartitaIVADestinatario ?? "").ToLower().Contains(s) ||
                (x.CodiceSDI ?? "").ToLower().Contains(s));
        }

        FeDataGrid.ItemsSource = await q.ToListAsync();
    }

    private async void BtnRicarica_Click(object sender, RoutedEventArgs e)
    {
        if (!IsFatturazioneElettronicaEnabled())
        {
            MessageBox.Show(
                "Fatturazione elettronica disabilitata.\n\nVai in Utility → Impostazioni e abilita 'Fatturazione elettronica'.",
                "DIDO-GEST",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        await LoadData();
    }

    private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if (!IsFatturazioneElettronicaEnabled()) return;
        await LoadData();
    }

    private async void BtnInviato_Click(object sender, RoutedEventArgs e)
    {
        if (!IsFatturazioneElettronicaEnabled())
        {
            MessageBox.Show(
                "Fatturazione elettronica disabilitata.\n\nVai in Utility → Impostazioni e abilita 'Fatturazione elettronica'.",
                "DIDO-GEST",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (FeDataGrid.SelectedItem is not Documento selected) return;

        var entity = await _context.Documenti.FirstOrDefaultAsync(d => d.Id == selected.Id);
        if (entity == null) return;

        entity.FatturaElettronica = true;
        entity.XMLInviato = true;
        entity.DataInvioXML = DateTime.Now;
        entity.StatoFatturaElettronica = string.IsNullOrWhiteSpace(entity.StatoFatturaElettronica) ? "INVIATA" : entity.StatoFatturaElettronica;

        await _context.SaveChangesAsync();
        await LoadData();
    }

    private async void BtnEsporta_Click(object sender, RoutedEventArgs e)
    {
        if (!IsFatturazioneElettronicaEnabled())
        {
            MessageBox.Show(
                "Fatturazione elettronica disabilitata.\n\nVai in Utility → Impostazioni e abilita 'Fatturazione elettronica'.",
                "DIDO-GEST",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (FeDataGrid.SelectedItem is not Documento selected) return;

        var entity = await _context.Documenti
            .AsNoTracking()
            .Include(d => d.Cliente)
            .FirstOrDefaultAsync(d => d.Id == selected.Id);

        if (entity == null) return;

        try
        {
            var (mode, settings) = GetFeSettings();
            var xml = BuildMinimalXml(entity);

            string? savedPath = null;

            if (string.Equals(mode, FeModeCommercialista, StringComparison.OrdinalIgnoreCase))
            {
                var folder = (settings.FeCartellaCommercialista ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(folder))
                {
                    MessageBox.Show(
                        "Cartella XML non configurata.\n\nVai in Utility → Impostazioni → Fatturazione elettronica e imposta 'Cartella XML (consegna al commercialista)'.",
                        "Fatturazione elettronica",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(folder);
                var fileName = $"{SanitizeFileName(entity.NumeroDocumento)}.xml";
                savedPath = Path.Combine(folder, fileName);
                File.WriteAllText(savedPath, xml, Encoding.UTF8);
            }
            else
            {
                var url = (settings.FeApiUrl ?? string.Empty).Trim();
                var token = (settings.FeApiKey ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show(
                        "URL API non configurato.\n\nVai in Utility → Impostazioni → Fatturazione elettronica e imposta 'URL API'.",
                        "Fatturazione elettronica",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(xml, Encoding.UTF8, "application/xml");
                if (!string.IsNullOrWhiteSpace(token))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    if (body.Length > 400) body = body[..400] + "...";
                    throw new InvalidOperationException($"Invio non riuscito (HTTP {(int)resp.StatusCode}). {body}".Trim());
                }
            }

            // aggiorna metadati
            var update = await _context.Documenti.FirstAsync(d => d.Id == entity.Id);
            update.FatturaElettronica = true;

            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                update.NomeFileXML = Path.GetFileName(savedPath);
                update.StatoFatturaElettronica = string.IsNullOrWhiteSpace(update.StatoFatturaElettronica) ? "GENERATA" : update.StatoFatturaElettronica;
            }
            else
            {
                update.XMLInviato = true;
                update.DataInvioXML = DateTime.Now;
                update.StatoFatturaElettronica = string.IsNullOrWhiteSpace(update.StatoFatturaElettronica) ? "INVIATA" : update.StatoFatturaElettronica;
            }
            await _context.SaveChangesAsync();

            await LoadData();
            MessageBox.Show(
                !string.IsNullOrWhiteSpace(savedPath) ? $"XML generato nella cartella configurata.\n\nFile: {savedPath}" : "XML inviato al server configurato.",
                "OK",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            UiLog.Error("FatturazioneElettronicaView.BtnEsporta_Click", ex);
            MessageBox.Show($"Errore generazione/invio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void FeDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FeDataGrid.SelectedItem is not Documento selected) return;

        var tipo = string.IsNullOrWhiteSpace(selected.TipoDocumento) ? "FATTURA" : selected.TipoDocumento;
        var w = new DocumentoEditWindow(_context, tipo, selected.Id);
        w.ShowDialog();

        await LoadData();
    }

    private static string BuildMinimalXml(Documento d)
    {
        // XML minimale (non sostituisce il tracciato ufficiale FatturaPA): serve a rendere il modulo operativo.
        var cliente = d.Cliente?.RagioneSociale ?? d.RagioneSocialeDestinatario ?? string.Empty;
        var piva = d.PartitaIVADestinatario ?? string.Empty;
        var sdi = d.CodiceSDI ?? string.Empty;

                return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<DidoGestFatturaElettronica>
  <Documento>
    <Tipo>{Escape(d.TipoDocumento)}</Tipo>
    <Numero>{Escape(d.NumeroDocumento)}</Numero>
    <Data>{d.DataDocumento:yyyy-MM-dd}</Data>
    <Imponibile>{d.Imponibile:0.00}</Imponibile>
    <Iva>{d.IVA:0.00}</Iva>
    <Totale>{d.Totale:0.00}</Totale>
  </Documento>
  <Destinatario>
    <RagioneSociale>{Escape(cliente)}</RagioneSociale>
    <PartitaIVA>{Escape(piva)}</PartitaIVA>
    <CodiceSDI>{Escape(sdi)}</CodiceSDI>
    <PEC>{Escape(d.PECDestinatario)}</PEC>
  </Destinatario>
</DidoGestFatturaElettronica>";
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
