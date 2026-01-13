using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using DidoGest.UI.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Documenti;

public partial class FattureView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<FatturaRowVm> _allFatture = new();
    private List<FatturaRowVm> _current = new();
    private bool _adjustingPeriodo;
    private bool _uiReady;

    public FattureView()
    {
        _context = DidoGestDb.CreateContext();
        InitializeComponent();
        
        Loaded += FattureView_Loaded;
    }

    private void SyncStampaIncassiUi()
    {
        var isPagate = GetFiltroStato() == FattureFiltroStato.Pagate;
        if (BtnStampaIncassi != null)
        {
            BtnStampaIncassi.IsEnabled = isPagate;
            BtnStampaIncassi.ToolTip = isPagate ? null : "Disponibile con filtro: Pagate";
        }

        if (DpPeriodoDa != null) DpPeriodoDa.IsEnabled = isPagate;
        if (DpPeriodoA != null) DpPeriodoA.IsEnabled = isPagate;

        try
        {
            if (DpPeriodoDa != null)
                HintAssist.SetHint(DpPeriodoDa, isPagate ? "Data pagamento da" : "Periodo (solo Pagate)");
            if (DpPeriodoA != null)
                HintAssist.SetHint(DpPeriodoA, isPagate ? "Data pagamento a" : "Periodo (solo Pagate)");
        }
        catch (Exception ex)
        {
            UiLog.Error("FattureView.SyncStampaIncassiUi", ex);
        }
    }

    private async void FattureView_Loaded(object sender, RoutedEventArgs e)
    {
        _uiReady = true;
        try
        {
            SyncStampaIncassiUi();
        }
        catch (Exception ex)
        {
            UiLog.Error("FattureView.Loaded", ex);
        }

        await LoadFatture();
    }

    private async Task LoadFatture()
    {
        try
        {
            var today = DateTime.Today;

            var fattureBase = await _context.Documenti
                .Include(d => d.Cliente)
                .Include(d => d.Fornitore)
                .Include(d => d.DocumentoOriginale)
                .Where(d => EF.Functions.Like(d.TipoDocumento, "%FATTURA%"))
                .OrderByDescending(d => d.DataDocumento)
                .Select(d => new
                {
                    d.Id,
                    d.NumeroDocumento,
                    d.DataDocumento,
                    ControparteRagioneSociale = d.Cliente != null
                        ? d.Cliente.RagioneSociale
                        : (d.Fornitore != null ? d.Fornitore.RagioneSociale : "N/D"),
                    ControparteTelefono = d.Cliente != null
                        ? d.Cliente.Telefono
                        : (d.Fornitore != null ? d.Fornitore.Telefono : null),
                    ControparteCellulare = d.Cliente != null ? d.Cliente.Cellulare : null,
                    ControparteEmail = d.Cliente != null
                        ? d.Cliente.Email
                        : (d.Fornitore != null ? d.Fornitore.Email : null),
                    ContropartePEC = d.Cliente != null
                        ? d.Cliente.PEC
                        : (d.Fornitore != null ? d.Fornitore.PEC : null),
                    TotaleDocumento = d.Totale,
                    d.TipoDocumento,
                    Emissione = d.DocumentoOriginaleId.HasValue ? "Differita" : "Immediata",
                    DdtOrigineNumero = d.DocumentoOriginale != null && d.DocumentoOriginale.TipoDocumento == "DDT"
                        ? d.DocumentoOriginale.NumeroDocumento
                        : string.Empty,
                    IsPagata = d.Pagato,
                    d.DataPagamento,
                    d.DataScadenzaPagamento,
                    d.ModalitaPagamento,
                    d.BancaAppoggio
                })
                .ToListAsync();

            var fattureIds = fattureBase.Select(x => x.Id).ToList();
            var links = await _context.DocumentoCollegamenti
                .AsNoTracking()
                .Include(l => l.DocumentoOrigine)
                .Where(l => fattureIds.Contains(l.DocumentoId))
                .Select(l => new
                {
                    l.DocumentoId,
                    DdtNumero = l.DocumentoOrigine != null && l.DocumentoOrigine.TipoDocumento == "DDT"
                        ? l.DocumentoOrigine.NumeroDocumento
                        : null
                })
                .ToListAsync();

            var mapMulti = links
                .Where(x => !string.IsNullOrWhiteSpace(x.DdtNumero))
                .GroupBy(x => x.DocumentoId)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(", ", g.Select(x => x.DdtNumero!).Distinct().OrderBy(s => s)));

            _allFatture = fattureBase
                .Select(x =>
                {
                    var ddt = mapMulti.TryGetValue(x.Id, out var multi) && !string.IsNullOrWhiteSpace(multi)
                        ? multi
                        : x.DdtOrigineNumero;

                    var scad = x.DataScadenzaPagamento?.Date;
                    int? giorni = scad.HasValue ? (int)(scad.Value - today).TotalDays : null;

                    return new FatturaRowVm
                    {
                        Id = x.Id,
                        NumeroDocumento = x.NumeroDocumento ?? string.Empty,
                        DataDocumento = x.DataDocumento,
                        ControparteRagioneSociale = x.ControparteRagioneSociale ?? "N/D",
                        ControparteTelefono = x.ControparteTelefono,
                        ControparteCellulare = x.ControparteCellulare,
                        ControparteEmail = x.ControparteEmail,
                        ContropartePEC = x.ContropartePEC,
                        TotaleDocumento = x.TotaleDocumento,
                        TipoDocumento = x.TipoDocumento ?? string.Empty,
                        Emissione = x.Emissione ?? string.Empty,
                        DdtOrigineNumero = ddt ?? string.Empty,
                        IsPagata = x.IsPagata,
                        DataPagamento = x.DataPagamento,
                        DataScadenzaPagamento = x.DataScadenzaPagamento,
                        GiorniAllaScadenza = giorni,
                        IsScaduta = !x.IsPagata && scad.HasValue && scad.Value < today,
                        ModalitaPagamento = x.ModalitaPagamento,
                        BancaAppoggio = x.BancaAppoggio,
                        StatoPagamento = GetStatoPagamento(x.IsPagata, scad)
                    };
                })
                .ToList();

            ApplyFiltersAndRefresh();
        }
        catch (Exception ex)
        {
            UiLog.Error("FattureView.LoadFatture", ex);
            MessageBox.Show($"Errore caricamento fatture: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFiltersAndRefresh()
    {
        if (!_uiReady) return;
        if (SearchBox == null || FattureDataGrid == null) return;

        if (TotaleFatture == null || TotaleMostrate == null || ImportoMostrato == null
            || NonPagateCount == null || NonPagateImporto == null
            || ScaduteCount == null || ScaduteImporto == null
            || PagateSummaryPanel == null || PagateCount == null || PagateImporto == null)
            return;

        var searchText = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        var filtro = GetFiltroStato();

        IEnumerable<FatturaRowVm> q = _allFatture;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            q = q.Where(f =>
                (f.NumeroDocumento ?? string.Empty).ToLowerInvariant().Contains(searchText)
                || (f.ControparteRagioneSociale ?? string.Empty).ToLowerInvariant().Contains(searchText));
        }

        var today = DateTime.Today;
        q = filtro switch
        {
            FattureFiltroStato.NonPagate => q.Where(f => !f.IsPagata),
            FattureFiltroStato.Pagate => q.Where(f => f.IsPagata),
            FattureFiltroStato.Scadute => q.Where(f => !f.IsPagata && f.DataScadenzaPagamento.HasValue && f.DataScadenzaPagamento.Value.Date < today),
            FattureFiltroStato.InScadenza7 => q.Where(f => !f.IsPagata && f.DataScadenzaPagamento.HasValue && f.DataScadenzaPagamento.Value.Date >= today && f.DataScadenzaPagamento.Value.Date <= today.AddDays(7)),
            FattureFiltroStato.InScadenza15 => q.Where(f => !f.IsPagata && f.DataScadenzaPagamento.HasValue && f.DataScadenzaPagamento.Value.Date >= today && f.DataScadenzaPagamento.Value.Date <= today.AddDays(15)),
            FattureFiltroStato.InScadenza30 => q.Where(f => !f.IsPagata && f.DataScadenzaPagamento.HasValue && f.DataScadenzaPagamento.Value.Date >= today && f.DataScadenzaPagamento.Value.Date <= today.AddDays(30)),
            _ => q
        };

        // Range periodo: solo quando filtro = Pagate (interpretato come DataPagamento)
        if (filtro == FattureFiltroStato.Pagate)
        {
            var da = DpPeriodoDa.SelectedDate?.Date;
            var a = DpPeriodoA.SelectedDate?.Date;

            if (da.HasValue)
                q = q.Where(f => f.DataPagamento.HasValue && f.DataPagamento.Value.Date >= da.Value);
            if (a.HasValue)
                q = q.Where(f => f.DataPagamento.HasValue && f.DataPagamento.Value.Date <= a.Value);
        }

        _current = filtro == FattureFiltroStato.Pagate
            ? q.OrderBy(f => f.DataPagamento.HasValue ? 0 : 1)
                .ThenByDescending(f => f.DataPagamento)
                .ThenByDescending(f => f.DataDocumento)
                .ToList()
            : q.OrderBy(f => f.DataScadenzaPagamento.HasValue ? 0 : 1)
                .ThenBy(f => f.DataScadenzaPagamento)
                .ThenByDescending(f => f.DataDocumento)
                .ToList();
        FattureDataGrid.ItemsSource = _current;

        SyncStampaIncassiUi();

        TotaleFatture.Text = _allFatture.Count.ToString();
        TotaleMostrate.Text = _current.Count.ToString();
        var totaleMostrato = _current.Sum(f => f.TotaleDocumento);
        ImportoMostrato.Text = totaleMostrato.ToString("C2");

        var nonPagate = _current.Where(f => !f.IsPagata).ToList();
        NonPagateCount.Text = nonPagate.Count.ToString();
        NonPagateImporto.Text = nonPagate.Sum(f => f.TotaleDocumento).ToString("C2");

        var scadute = _current.Where(f => f.IsScaduta).ToList();
        ScaduteCount.Text = scadute.Count.ToString();
        ScaduteImporto.Text = scadute.Sum(f => f.TotaleDocumento).ToString("C2");

        // Riepilogo incassate: solo su filtro Pagate (periodo)
        PagateSummaryPanel.Visibility = filtro == FattureFiltroStato.Pagate ? Visibility.Visible : Visibility.Collapsed;
        if (filtro == FattureFiltroStato.Pagate)
        {
            PagateCount.Text = _current.Count.ToString();
            PagateImporto.Text = _current.Sum(f => f.TotaleDocumento).ToString("C2");
        }
    }

    private static string GetStatoPagamento(bool pagata, DateTime? scadenza)
    {
        if (pagata) return "Pagata";
        if (!scadenza.HasValue) return "Da pagare";

        var today = DateTime.Today;
        var d = scadenza.Value.Date;
        if (d < today) return "Scaduta";
        if (d <= today.AddDays(7)) return "In scadenza";
        return "Da pagare";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady) return;
        ApplyFiltersAndRefresh();
    }

    private void FilterStatoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        SyncStampaIncassiUi();
        ApplyFiltersAndRefresh();
    }

    private void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        if (_adjustingPeriodo) return;

        if (GetFiltroStato() == FattureFiltroStato.Pagate)
        {
            var da = DpPeriodoDa.SelectedDate?.Date;
            var a = DpPeriodoA.SelectedDate?.Date;
            if (da.HasValue && a.HasValue && da.Value > a.Value)
            {
                _adjustingPeriodo = true;
                try
                {
                    DpPeriodoDa.SelectedDate = a;
                    DpPeriodoA.SelectedDate = da;
                }
                finally
                {
                    _adjustingPeriodo = false;
                }
            }
        }

        ApplyFiltersAndRefresh();
    }

    private void BtnStampaIncassi_Click(object sender, RoutedEventArgs e)
    {
        if (GetFiltroStato() != FattureFiltroStato.Pagate)
        {
            MessageBox.Show("Imposta il filtro su 'Pagate' per stampare gli incassi.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner == null) return;

        var pagate = _current.Where(r => r.IsPagata).Cast<object>().ToList();
        if (pagate.Count == 0)
        {
            MessageBox.Show("Nessuna fattura pagata da stampare con i filtri correnti.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var settings = new AppSettingsService().Load();
        var da = DpPeriodoDa.SelectedDate?.Date;
        var a = DpPeriodoA.SelectedDate?.Date;
        IncassiPrintService.PrintIncassi(owner, pagate, settings, da, a);
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new DocumentoEditWindow(_context, "FATTURA", null);
        if (w.ShowDialog() == true)
            _ = LoadFatture();
    }

    private void BtnVisualizza_Click(object sender, RoutedEventArgs e)
    {
        if (FattureDataGrid.SelectedItem != null)
        {
            var fattura = (FatturaRowVm)FattureDataGrid.SelectedItem;
            var w = new DocumentoEditWindow(_context, "FATTURA", fattura.Id);
            if (w.ShowDialog() == true)
                _ = LoadFatture();
        }
        else
        {
            MessageBox.Show("Seleziona una fattura", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void FattureDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FattureDataGrid.SelectedItem == null) return;

        // Riusa la stessa logica del pulsante Visualizza
        BtnVisualizza_Click(sender, e);
    }

    private void FattureDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FattureDataGrid.SelectedItem is not FatturaRowVm row)
        {
            DpScadenzaQuick.SelectedDate = null;
            return;
        }

        DpScadenzaQuick.SelectedDate = row.DataScadenzaPagamento?.Date;
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (FattureDataGrid.SelectedItem != null)
        {
            var fattura = (FatturaRowVm)FattureDataGrid.SelectedItem;
            var result = MessageBox.Show(
                $"Vuoi eliminare la fattura {fattura.NumeroDocumento}?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var doc = await _context.Documenti.FindAsync(fattura.Id);
                    if (doc != null)
                    {
                        _context.Documenti.Remove(doc);
                        await _context.SaveChangesAsync();
                        await LoadFatture();
                        MessageBox.Show("Fattura eliminata", "Successo", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    UiLog.Error("FattureView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Seleziona una fattura da eliminare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnSegnaPagata_Click(object sender, RoutedEventArgs e)
    {
        await SetPagatoAsync(true);
    }

    private async void BtnSegnaNonPagata_Click(object sender, RoutedEventArgs e)
    {
        await SetPagatoAsync(false);
    }

    private async void MenuSegnaPagata_Click(object sender, RoutedEventArgs e)
    {
        await SetPagatoAsync(true);
    }

    private async void MenuSegnaNonPagata_Click(object sender, RoutedEventArgs e)
    {
        await SetPagatoAsync(false);
    }

    private async void BtnImpostaScadenza_Click(object sender, RoutedEventArgs e)
    {
        await SetScadenzaAsync(imposta: true);
    }

    private async void BtnSvuotaScadenza_Click(object sender, RoutedEventArgs e)
    {
        await SetScadenzaAsync(imposta: false);
    }

    private async void MenuImpostaScadenza_Click(object sender, RoutedEventArgs e)
    {
        await SetScadenzaAsync(imposta: true);
    }

    private async void MenuSvuotaScadenza_Click(object sender, RoutedEventArgs e)
    {
        await SetScadenzaAsync(imposta: false);
    }

    private async Task SetPagatoAsync(bool pagato)
    {
        if (FattureDataGrid.SelectedItem == null)
        {
            MessageBox.Show("Seleziona una fattura.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fattura = (FatturaRowVm)FattureDataGrid.SelectedItem;

        try
        {
            var doc = await _context.Documenti.FindAsync(fattura.Id);
            if (doc == null) return;

            if (doc.Pagato == pagato)
            {
                MessageBox.Show(
                    pagato ? "La fattura risulta già pagata." : "La fattura risulta già non pagata.",
                    "DIDO-GEST",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            doc.Pagato = pagato;
            doc.DataPagamento = pagato ? DateTime.Today : null;
            doc.DataModifica = DateTime.Now;
            await _context.SaveChangesAsync();
            await LoadFatture();
        }
        catch (Exception ex)
        {
            UiLog.Error("FattureView.SetPagatoAsync", ex);
            MessageBox.Show($"Errore aggiornamento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SetScadenzaAsync(bool imposta)
    {
        if (FattureDataGrid.SelectedItem == null)
        {
            MessageBox.Show("Seleziona una fattura.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (imposta && !DpScadenzaQuick.SelectedDate.HasValue)
        {
            MessageBox.Show("Seleziona una data di scadenza.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var fattura = (FatturaRowVm)FattureDataGrid.SelectedItem;

        try
        {
            var doc = await _context.Documenti.FindAsync(fattura.Id);
            if (doc == null) return;

            if (imposta)
            {
                var nuova = DpScadenzaQuick.SelectedDate!.Value.Date;
                if (doc.DataScadenzaPagamento.HasValue && doc.DataScadenzaPagamento.Value.Date == nuova)
                {
                    MessageBox.Show("La scadenza è già impostata su questa data.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                doc.DataScadenzaPagamento = nuova;
            }
            else
            {
                if (!doc.DataScadenzaPagamento.HasValue)
                {
                    MessageBox.Show("La fattura non ha una scadenza impostata.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                doc.DataScadenzaPagamento = null;
            }

            doc.DataModifica = DateTime.Now;
            await _context.SaveChangesAsync();
            await LoadFatture();
        }
        catch (Exception ex)
        {
            UiLog.Error("FattureView.SetScadenzaAsync", ex);
            MessageBox.Show($"Errore aggiornamento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FattureFiltroStato GetFiltroStato()
    {
        if (FilterStatoCombo == null) return FattureFiltroStato.Tutte;
        var content = (FilterStatoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tutte";
        return content switch
        {
            "Non pagate" => FattureFiltroStato.NonPagate,
            "Pagate" => FattureFiltroStato.Pagate,
            "Scadute" => FattureFiltroStato.Scadute,
            "In scadenza (7gg)" => FattureFiltroStato.InScadenza7,
            "In scadenza (15gg)" => FattureFiltroStato.InScadenza15,
            "In scadenza (30gg)" => FattureFiltroStato.InScadenza30,
            _ => FattureFiltroStato.Tutte
        };
    }

    private enum FattureFiltroStato
    {
        Tutte = 0,
        NonPagate = 1,
        Pagate = 2,
        Scadute = 3,
        InScadenza7 = 4,
        InScadenza15 = 5,
        InScadenza30 = 6
    }

    private sealed class FatturaRowVm
    {
        public int Id { get; set; }
        public string NumeroDocumento { get; set; } = string.Empty;
        public DateTime DataDocumento { get; set; }
        public string ControparteRagioneSociale { get; set; } = string.Empty;
        public string? ControparteTelefono { get; set; }
        public string? ControparteCellulare { get; set; }
        public string? ControparteEmail { get; set; }
        public string? ContropartePEC { get; set; }
        public decimal TotaleDocumento { get; set; }
        public string TipoDocumento { get; set; } = string.Empty;
        public string Emissione { get; set; } = string.Empty;
        public string DdtOrigineNumero { get; set; } = string.Empty;
        public bool IsPagata { get; set; }
        public DateTime? DataPagamento { get; set; }
        public DateTime? DataScadenzaPagamento { get; set; }
        public bool IsScaduta { get; set; }
        public int? GiorniAllaScadenza { get; set; }
        public string? StatoPagamento { get; set; }
        public string? ModalitaPagamento { get; set; }
        public string? BancaAppoggio { get; set; }
    }
}
