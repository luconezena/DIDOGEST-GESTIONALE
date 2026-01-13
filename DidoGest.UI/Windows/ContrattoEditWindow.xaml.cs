using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class ContrattoEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _id;

    public ContrattoEditWindow(int? id)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;

        _context = DidoGestDb.CreateContext();

        _id = id;
        Loaded += async (_, _) => await Init();
    }

    private async Task Init()
    {
        var clienti = await _context.Clienti.AsNoTracking().OrderBy(c => c.RagioneSociale).ToListAsync();
        CmbCliente.ItemsSource = clienti;
        CmbCliente.DisplayMemberPath = "RagioneSociale";

        CmbTipo.ItemsSource = new[] { "TEMPO_DETERMINATO", "MONTE_ORE" };
        CmbStato.ItemsSource = new[] { "ATTIVO", "SCADUTO", "ANNULLATO" };
        CmbTipo.SelectedIndex = 0;
        CmbStato.SelectedIndex = 0;

        if (_id.HasValue)
        {
            var entity = await _context.Contratti.FirstOrDefaultAsync(x => x.Id == _id.Value);
            if (entity == null) return;

            TxtNumero.Text = entity.NumeroContratto ?? string.Empty;
            TxtDescrizione.Text = entity.Descrizione ?? string.Empty;
            DpInizio.SelectedDate = entity.DataInizio == default ? DateTime.Today : entity.DataInizio;
            DpFine.SelectedDate = entity.DataFine;

            TxtImporto.Text = entity.Importo.ToString("0.00", CultureInfo.InvariantCulture);
            TxtCostoExtra.Text = (entity.CostoOrarioExtra ?? 0m).ToString("0.00", CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(entity.TipoContratto))
                CmbTipo.SelectedItem = entity.TipoContratto;
            if (!string.IsNullOrWhiteSpace(entity.StatoContratto))
                CmbStato.SelectedItem = entity.StatoContratto;

            TxtMonteOreAcq.Text = entity.MonteOreAcquistato?.ToString() ?? string.Empty;
            TxtMonteOreRes.Text = entity.MonteOreResiduo?.ToString() ?? string.Empty;
            TxtFreq.Text = entity.FrequenzaFatturazione ?? string.Empty;
            DpProssimaFatt.SelectedDate = entity.ProssimaFatturazione;
            TxtNote.Text = entity.Note ?? string.Empty;

            CmbCliente.SelectedItem = clienti.FirstOrDefault(c => c.Id == entity.ClienteId);
        }
        else
        {
            TxtNumero.Text = $"CTR-{DateTime.Now:yyyyMMddHHmm}";
            DpInizio.SelectedDate = DateTime.Today;
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (CmbCliente.SelectedItem is not Cliente cliente)
            {
                MessageBox.Show("Seleziona un cliente.", "Dati mancanti", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Contratto entity;
            if (_id.HasValue)
            {
                entity = await _context.Contratti.FirstAsync(x => x.Id == _id.Value);
            }
            else
            {
                entity = new Contratto();
                _context.Contratti.Add(entity);
            }

            entity.NumeroContratto = (TxtNumero.Text ?? string.Empty).Trim();
            entity.ClienteId = cliente.Id;
            entity.Descrizione = (TxtDescrizione.Text ?? string.Empty).Trim();
            entity.DataInizio = DpInizio.SelectedDate ?? DateTime.Today;
            entity.DataFine = DpFine.SelectedDate;
            entity.TipoContratto = CmbTipo.SelectedItem as string;
            entity.StatoContratto = CmbStato.SelectedItem as string;

            entity.Importo = ParseDecimal(TxtImporto.Text);
            entity.CostoOrarioExtra = ParseNullableDecimal(TxtCostoExtra.Text);

            entity.MonteOreAcquistato = ParseNullableInt(TxtMonteOreAcq.Text);
            entity.MonteOreResiduo = ParseNullableInt(TxtMonteOreRes.Text);
            entity.FrequenzaFatturazione = (TxtFreq.Text ?? string.Empty).Trim();
            entity.ProssimaFatturazione = DpProssimaFatt.SelectedDate;
            entity.Note = (TxtNote.Text ?? string.Empty).Trim();

            await _context.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("ContrattoEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static decimal ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var t = text.Trim().Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static decimal? ParseNullableDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim().Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static int? ParseNullableInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return int.TryParse(text.Trim(), out var v) ? v : null;
    }
}
