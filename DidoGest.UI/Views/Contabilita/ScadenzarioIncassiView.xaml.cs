using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Services;
using DidoGest.UI.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Contabilita;

public partial class ScadenzarioIncassiView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<ScadenzaRowVm> _all = new();
    private List<ScadenzaRowVm> _current = new();
    private bool _adjustingPeriodo;
    private bool _uiReady;

    public ScadenzarioIncassiView()
    {
        _context = DidoGestDb.CreateContext();
        InitializeComponent();
        Loaded += ScadenzarioIncassiView_Loaded;
    }

    private void SyncPeriodoUiForFiltroPagato()
    {
        var filtro = GetFiltroPagato();
        var isPagate = filtro == FiltroPagato.Pagate;

        if (DpPeriodoDa != null)
            HintAssist.SetHint(DpPeriodoDa, isPagate ? "Data pagamento da" : "Data doc da");
        if (DpPeriodoA != null)
            HintAssist.SetHint(DpPeriodoA, isPagate ? "Data pagamento a" : "Data doc a");

        if (BtnStampaIncassi != null)
        {
            BtnStampaIncassi.IsEnabled = isPagate;
            BtnStampaIncassi.ToolTip = isPagate ? null : "Disponibile con filtro: Pagate";
        }
    }

    private async void ScadenzarioIncassiView_Loaded(object sender, RoutedEventArgs e)
    {
        _uiReady = true;
        try
        {
            SyncPeriodoUiForFiltroPagato();
        }
        catch
        {
            // best effort: non bloccare la vista per un problema di HintAssist
        }

        await LoadScadenzario();
    }

    private async Task LoadScadenzario()
    {
        try
        {
            var today = DateTime.Today;
            var filtroPagato = GetFiltroPagato();
            var da = DpPeriodoDa?.SelectedDate?.Date;
            var a = DpPeriodoA?.SelectedDate?.Date;

            if (da.HasValue && a.HasValue && da.Value > a.Value)
            {
                _adjustingPeriodo = true;
                try
                {
                    if (DpPeriodoDa != null) DpPeriodoDa.SelectedDate = a;
                    if (DpPeriodoA != null) DpPeriodoA.SelectedDate = da;
                }
                finally
                {
                    _adjustingPeriodo = false;
                }

                (da, a) = (a, da);
            }

            // Range date richiesto soprattutto per le pagate: qui lo applichiamo quando la vista include pagate.
            var applyDateRange = filtroPagato is FiltroPagato.Pagate or FiltroPagato.Tutte;

            var q = _context.Documenti
                .AsNoTracking()
                .Include(d => d.Cliente)
                .Include(d => d.Fornitore)
                .Include(d => d.DocumentoOriginale)
                .Where(d => EF.Functions.Like(d.TipoDocumento, "%FATTURA%"));

            q = filtroPagato switch
            {
                FiltroPagato.NonPagate => q.Where(d => !d.Pagato),
                FiltroPagato.Pagate => q.Where(d => d.Pagato),
                _ => q
            };

            if (applyDateRange)
            {
                // Quando sto guardando le pagate, il range deve essere sulla DataPagamento (incasso), non sulla data documento.
                if (filtroPagato == FiltroPagato.Pagate)
                {
                    if (da.HasValue) q = q.Where(d => d.DataPagamento.HasValue && d.DataPagamento.Value.Date >= da.Value);
                    if (a.HasValue) q = q.Where(d => d.DataPagamento.HasValue && d.DataPagamento.Value.Date <= a.Value);
                }
                else
                {
                    if (da.HasValue) q = q.Where(d => d.DataDocumento.Date >= da.Value);
                    if (a.HasValue) q = q.Where(d => d.DataDocumento.Date <= a.Value);
                }
            }

            var baseRows = await q
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
                    ControparteCellulare = d.Cliente != null
                        ? d.Cliente.Cellulare
                        : null,
                    ControparteEmail = d.Cliente != null
                        ? d.Cliente.Email
                        : (d.Fornitore != null ? d.Fornitore.Email : null),
                    ContropartePEC = d.Cliente != null
                        ? d.Cliente.PEC
                        : (d.Fornitore != null ? d.Fornitore.PEC : null),
                    TotaleDocumento = d.Totale,
                    d.TipoDocumento,
                    DdtOrigineNumero = d.DocumentoOriginale != null && d.DocumentoOriginale.TipoDocumento == "DDT"
                        ? d.DocumentoOriginale.NumeroDocumento
                        : string.Empty,
                    d.DataScadenzaPagamento,
                    d.ModalitaPagamento,
                    d.BancaAppoggio,
                    d.Pagato,
                    d.DataPagamento
                })
                .ToListAsync();

            var ids = baseRows.Select(x => x.Id).ToList();
            var links = await _context.DocumentoCollegamenti
                .AsNoTracking()
                .Include(l => l.DocumentoOrigine)
                .Where(l => ids.Contains(l.DocumentoId))
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

            _all = baseRows
                .Select(x =>
                {
                    var ddt = mapMulti.TryGetValue(x.Id, out var multi) && !string.IsNullOrWhiteSpace(multi)
                        ? multi
                        : x.DdtOrigineNumero;

                    var scad = x.DataScadenzaPagamento?.Date;
                    int? giorni = scad.HasValue ? (int)(scad.Value - today).TotalDays : null;

                    return new ScadenzaRowVm
                    {
                        Id = x.Id,
                        NumeroDocumento = x.NumeroDocumento ?? string.Empty,
                        DataDocumento = x.DataDocumento,
                        ControparteRagioneSociale = x.ControparteRagioneSociale ?? "N/D",
                        TotaleDocumento = x.TotaleDocumento,
                        TipoDocumento = x.TipoDocumento ?? string.Empty,
                        DataScadenzaPagamento = x.DataScadenzaPagamento,
                        GiorniAllaScadenza = giorni,
                        Pagato = x.Pagato,
                        DataPagamento = x.DataPagamento,
                        StatoPagamento = GetStato(x.Pagato, scad),
                        ModalitaPagamento = x.ModalitaPagamento,
                        BancaAppoggio = x.BancaAppoggio,
                        DdtOrigineNumero = ddt ?? string.Empty,
                        ControparteTelefono = x.ControparteTelefono,
                        ControparteCellulare = x.ControparteCellulare,
                        ControparteEmail = x.ControparteEmail,
                        ContropartePEC = x.ContropartePEC
                    };
                })
                .ToList();

            ApplyFiltersAndRefresh();
        }
        catch (Exception ex)
        {
            UiLog.Error("ScadenzarioIncassiView.LoadScadenzario", ex);
            MessageBox.Show($"Errore caricamento scadenzario: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFiltersAndRefresh()
    {
        if (!_uiReady) return;
        if (SearchBox == null || FilterStatoCombo == null || ScadenzarioDataGrid == null) return;

        var search = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();
        var filtro = GetFiltroStato();
        var filtroPagato = GetFiltroPagato();

        // Le viste "Pagate" non hanno senso con filtri scadenze: le disattiviamo.
        FilterStatoCombo.IsEnabled = filtroPagato != FiltroPagato.Pagate;
        if (filtroPagato == FiltroPagato.Pagate)
            filtro = FiltroStato.Tutte;

        IEnumerable<ScadenzaRowVm> q = _all;

        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(r =>
                (r.NumeroDocumento ?? string.Empty).ToLowerInvariant().Contains(search)
                || (r.ControparteRagioneSociale ?? string.Empty).ToLowerInvariant().Contains(search));
        }

        var today = DateTime.Today;
        q = filtro switch
        {
            FiltroStato.Scadute => q.Where(r => !r.Pagato && r.DataScadenzaPagamento.HasValue && r.DataScadenzaPagamento.Value.Date < today),
            FiltroStato.InScadenza7 => q.Where(r => !r.Pagato && r.DataScadenzaPagamento.HasValue && r.DataScadenzaPagamento.Value.Date >= today && r.DataScadenzaPagamento.Value.Date <= today.AddDays(7)),
            FiltroStato.InScadenza15 => q.Where(r => !r.Pagato && r.DataScadenzaPagamento.HasValue && r.DataScadenzaPagamento.Value.Date >= today && r.DataScadenzaPagamento.Value.Date <= today.AddDays(15)),
            FiltroStato.InScadenza30 => q.Where(r => !r.Pagato && r.DataScadenzaPagamento.HasValue && r.DataScadenzaPagamento.Value.Date >= today && r.DataScadenzaPagamento.Value.Date <= today.AddDays(30)),
            FiltroStato.SenzaScadenza => q.Where(r => !r.Pagato && !r.DataScadenzaPagamento.HasValue),
            _ => q
        };

        if (filtroPagato == FiltroPagato.Pagate)
        {
            // Storico incassi: ordinamento per data pagamento (null in fondo), poi cliente e numero.
            _current = q
                .OrderBy(r => r.DataPagamento.HasValue ? 0 : 1)
                .ThenByDescending(r => r.DataPagamento)
                .ThenBy(r => r.ControparteRagioneSociale)
                .ThenByDescending(r => r.DataDocumento)
                .ToList();
        }
        else
        {
            _current = q
                .OrderBy(r => r.DataScadenzaPagamento.HasValue ? 0 : 1)
                .ThenBy(r => r.DataScadenzaPagamento)
                .ThenBy(r => r.ControparteRagioneSociale)
                .ToList();
        }

        ScadenzarioDataGrid.ItemsSource = _current;

        TotaleMostrate.Text = _current.Count.ToString();
        ImportoMostrato.Text = _current.Sum(x => x.TotaleDocumento).ToString("C2");

        // Riepiloghi footer: scadute solo su non pagate/tutte; pagate solo su pagate/tutte.
        ScaduteSummaryPanel.Visibility = filtroPagato == FiltroPagato.Pagate ? Visibility.Collapsed : Visibility.Visible;
        PagateSummaryPanel.Visibility = filtroPagato == FiltroPagato.NonPagate ? Visibility.Collapsed : Visibility.Visible;

        var scadute = _current.Where(x => !x.Pagato && x.DataScadenzaPagamento.HasValue && x.DataScadenzaPagamento.Value.Date < today).ToList();
        ScaduteCount.Text = scadute.Count.ToString();
        ScaduteImporto.Text = scadute.Sum(x => x.TotaleDocumento).ToString("C2");

        var pagate = _current.Where(x => x.Pagato).ToList();
        PagateCount.Text = pagate.Count.ToString();
        PagateImporto.Text = pagate.Sum(x => x.TotaleDocumento).ToString("C2");
    }

    private static string GetStato(bool pagato, DateTime? scadenza)
    {
        if (pagato) return "Pagata";
        if (!scadenza.HasValue) return "Senza scadenza";
        var today = DateTime.Today;
        var d = scadenza.Value.Date;
        if (d < today) return "Scaduta";
        if (d <= today.AddDays(7)) return "In scadenza";
        return "Da pagare";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFiltersAndRefresh();
    }

    private void FilterStatoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyFiltersAndRefresh();
    }

    private async void FilterPagatoCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncPeriodoUiForFiltroPagato();
        await LoadScadenzario();
    }

    private async void Periodo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_adjustingPeriodo) return;
        await LoadScadenzario();
    }

    private FiltroStato GetFiltroStato()
    {
        if (FilterStatoCombo == null) return FiltroStato.Tutte;
        var content = (FilterStatoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
        return content switch
        {
            "Scadute" => FiltroStato.Scadute,
            "In scadenza (7gg)" => FiltroStato.InScadenza7,
            "In scadenza (15gg)" => FiltroStato.InScadenza15,
            "In scadenza (30gg)" => FiltroStato.InScadenza30,
            "Senza scadenza" => FiltroStato.SenzaScadenza,
            _ => FiltroStato.Tutte
        };
    }

    private FiltroPagato GetFiltroPagato()
    {
        if (FilterPagatoCombo == null) return FiltroPagato.NonPagate;
        var content = (FilterPagatoCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Non pagate";
        return content switch
        {
            "Pagate" => FiltroPagato.Pagate,
            "Tutte" => FiltroPagato.Tutte,
            _ => FiltroPagato.NonPagate
        };
    }

    private enum FiltroStato
    {
        Tutte = 0,
        Scadute = 1,
        InScadenza7 = 2,
        InScadenza15 = 3,
        InScadenza30 = 4,
        SenzaScadenza = 5
    }

    private enum FiltroPagato
    {
        NonPagate = 0,
        Pagate = 1,
        Tutte = 2
    }

    private void BtnVisualizza_Click(object sender, RoutedEventArgs e)
    {
        if (ScadenzarioDataGrid.SelectedItem is not ScadenzaRowVm row)
        {
            MessageBox.Show("Seleziona una fattura.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var w = new DocumentoEditWindow(_context, "FATTURA", row.Id);
        if (w.ShowDialog() == true)
            _ = LoadScadenzario();
    }

    private async void BtnSegnaPagata_Click(object sender, RoutedEventArgs e)
    {
        if (ScadenzarioDataGrid.SelectedItem is not ScadenzaRowVm row)
        {
            MessageBox.Show("Seleziona una fattura.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var doc = await _context.Documenti.FindAsync(row.Id);
            if (doc == null) return;

            if (doc.Pagato)
            {
                MessageBox.Show("La fattura risulta già pagata.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            doc.Pagato = true;
            doc.DataPagamento = doc.DataPagamento ?? DateTime.Today;
            doc.DataModifica = DateTime.Now;
            await _context.SaveChangesAsync();
            await LoadScadenzario();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore aggiornamento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnSegnaNonPagata_Click(object sender, RoutedEventArgs e)
    {
        if (ScadenzarioDataGrid.SelectedItem is not ScadenzaRowVm row)
        {
            MessageBox.Show("Seleziona una fattura.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var doc = await _context.Documenti.FindAsync(row.Id);
            if (doc == null) return;

            if (!doc.Pagato)
            {
                MessageBox.Show("La fattura risulta già non pagata.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            doc.Pagato = false;
            doc.DataPagamento = null;
            doc.DataModifica = DateTime.Now;
            await _context.SaveChangesAsync();
            await LoadScadenzario();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore aggiornamento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnImpostaScadenza_Click(object sender, RoutedEventArgs e)
    {
        if (ScadenzarioDataGrid.SelectedItem is not ScadenzaRowVm row)
        {
            MessageBox.Show("Seleziona una fattura.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!DpScadenzaQuick.SelectedDate.HasValue)
        {
            MessageBox.Show("Seleziona una data di scadenza.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var doc = await _context.Documenti.FindAsync(row.Id);
            if (doc == null) return;

            doc.DataScadenzaPagamento = DpScadenzaQuick.SelectedDate.Value.Date;
            doc.DataModifica = DateTime.Now;
            await _context.SaveChangesAsync();
            await LoadScadenzario();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore aggiornamento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnSvuotaScadenza_Click(object sender, RoutedEventArgs e)
    {
        if (ScadenzarioDataGrid.SelectedItem is not ScadenzaRowVm row)
        {
            MessageBox.Show("Seleziona una fattura.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var doc = await _context.Documenti.FindAsync(row.Id);
            if (doc == null) return;

            if (!doc.DataScadenzaPagamento.HasValue)
            {
                MessageBox.Show("La fattura non ha una scadenza impostata.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            doc.DataScadenzaPagamento = null;
            doc.DataModifica = DateTime.Now;
            await _context.SaveChangesAsync();
            await LoadScadenzario();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore aggiornamento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ScadenzarioDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnVisualizza_Click(sender, e);
    }

    private void ScadenzarioDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScadenzarioDataGrid.SelectedItem is not ScadenzaRowVm row)
        {
            DpScadenzaQuick.SelectedDate = null;
            return;
        }

        DpScadenzaQuick.SelectedDate = row.DataScadenzaPagamento?.Date;
    }

    private void BtnStampaSolleciti_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner == null) return;

        var settings = new AppSettingsService().Load();
        var daSollecitare = _current.Where(r => !r.Pagato).Cast<object>().ToList();
        if (daSollecitare.Count == 0)
        {
            MessageBox.Show("Nessuna fattura non pagata da sollecitare con i filtri correnti.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SollecitiPrintService.PrintSolleciti(owner, daSollecitare, settings);
    }

    private void BtnStampaIncassi_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner == null) return;

        var pagate = _current.Where(r => r.Pagato).Cast<object>().ToList();
        if (pagate.Count == 0)
        {
            MessageBox.Show("Nessuna fattura pagata da stampare con i filtri correnti.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var settings = new AppSettingsService().Load();

        // In modalità Pagate, il range da/a è già interpretato come DataPagamento; in Tutte rimane sul documento.
        var filtroPagato = GetFiltroPagato();
        var da = DpPeriodoDa.SelectedDate?.Date;
        var a = DpPeriodoA.SelectedDate?.Date;
        if (filtroPagato != FiltroPagato.Pagate)
        {
            // per stampe incassi fuori da Pagate evitiamo di stampare un periodo “finto”
            da = null;
            a = null;
        }

        IncassiPrintService.PrintIncassi(owner, pagate, settings, da, a);
    }

    private sealed class ScadenzaRowVm
    {
        public int Id { get; set; }
        public string NumeroDocumento { get; set; } = string.Empty;
        public DateTime DataDocumento { get; set; }
        public string ControparteRagioneSociale { get; set; } = string.Empty;
        public decimal TotaleDocumento { get; set; }
        public string TipoDocumento { get; set; } = string.Empty;
        public DateTime? DataScadenzaPagamento { get; set; }
        public int? GiorniAllaScadenza { get; set; }
        public string StatoPagamento { get; set; } = string.Empty;
        public string? ModalitaPagamento { get; set; }
        public string? BancaAppoggio { get; set; }
        public string DdtOrigineNumero { get; set; } = string.Empty;

        public bool Pagato { get; set; }
        public DateTime? DataPagamento { get; set; }
        public string? ControparteTelefono { get; set; }
        public string? ControparteCellulare { get; set; }
        public string? ControparteEmail { get; set; }
        public string? ContropartePEC { get; set; }
    }
}
