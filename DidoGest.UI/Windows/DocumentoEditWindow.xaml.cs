using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.Data.Services;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class DocumentoEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _documentoId;
    private readonly string _tipoDocumento;
    private Documento? _documento;

    private bool _suppressPartySelection;

    private List<Articolo> _articoli = new();
    private readonly ObservableCollection<DocumentoRigaVm> _righe = new();

    public IEnumerable<Articolo> ArticoliForCombo => _articoli;

    public DocumentoEditWindow(DidoGestDbContext context, string tipoDocumento, int? documentoId = null)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        _context = context;
        _tipoDocumento = tipoDocumento;
        _documentoId = documentoId;

        Loaded += DocumentoEditWindow_Loaded;
    }

    private async void DocumentoEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadClienti();
        await LoadFornitori();
        await LoadMagazzini();
        await LoadArticoli();

        RigheDataGrid.ItemsSource = _righe;

        TxtTipo.Text = _tipoDocumento;
        Header.Text = _tipoDocumento;

        if (_documentoId.HasValue)
        {
            _documento = await _context.Documenti
                .Include(d => d.Cliente)
                .Include(d => d.Righe)
                .FirstOrDefaultAsync(d => d.Id == _documentoId.Value);

            if (_documento == null)
            {
                MessageBox.Show("Documento non trovato.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
                Close();
                return;
            }

            Title = $"Modifica {_tipoDocumento}";
            LoadDocumento();
            LoadRighe();
        }
        else
        {
            Title = $"Nuovo {_tipoDocumento}";
            DpData.SelectedDate = DateTime.Today;
            TxtNumero.Text = await GeneraNuovoNumero();
            TxtImponibile.Text = "0";
            TxtIva.Text = "0";
            TxtTotale.Text = "0";
            TxtSconto.Text = "0";
            TxtSpese.Text = "0";

            _righe.Clear();
        }
    }

    private async Task LoadClienti()
    {
        var clienti = await _context.Clienti.AsNoTracking().OrderBy(c => c.RagioneSociale).ToListAsync();
        CmbCliente.ItemsSource = clienti;
        CmbCliente.DisplayMemberPath = "RagioneSociale";
        CmbCliente.SelectedValuePath = "Id";

        CmbCliente.SelectionChanged -= CmbCliente_SelectionChanged;
        CmbCliente.SelectionChanged += CmbCliente_SelectionChanged;
    }

    private async Task LoadFornitori()
    {
        var fornitori = await _context.Fornitori.AsNoTracking().OrderBy(f => f.RagioneSociale).ToListAsync();
        CmbFornitore.ItemsSource = fornitori;
        CmbFornitore.DisplayMemberPath = "RagioneSociale";
        CmbFornitore.SelectedValuePath = "Id";

        CmbFornitore.SelectionChanged -= CmbFornitore_SelectionChanged;
        CmbFornitore.SelectionChanged += CmbFornitore_SelectionChanged;
    }

    private async Task LoadMagazzini()
    {
        var mags = await _context.Magazzini
            .AsNoTracking()
            .Where(m => m.Attivo)
            .OrderByDescending(m => m.Principale)
            .ThenBy(m => m.Codice)
            .ToListAsync();

        var items = mags
            .Select(m => new ComboItem { Id = m.Id, Display = $"{m.Codice} - {m.Descrizione}" })
            .ToList();

        CmbMagazzino.ItemsSource = items;
        CmbMagazzino.SelectedValuePath = "Id";

        if (items.Count > 0)
            CmbMagazzino.SelectedValue = items[0].Id;
        else
            CmbMagazzino.SelectedIndex = -1;
    }

    private void CmbCliente_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressPartySelection) return;
        if (CmbCliente.SelectedValue is int)
        {
            _suppressPartySelection = true;
            CmbFornitore.SelectedItem = null;
            _suppressPartySelection = false;

            TryAutoSetScadenzaPagamentoFromCliente();
        }
    }

    private void CmbFornitore_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressPartySelection) return;
        if (CmbFornitore.SelectedValue is int)
        {
            _suppressPartySelection = true;
            CmbCliente.SelectedItem = null;
            _suppressPartySelection = false;
        }
    }

    private async Task LoadArticoli()
    {
        _articoli = await _context.Articoli.AsNoTracking().OrderBy(a => a.Codice).ToListAsync();
        // serve per il binding del ComboBox nel DataGrid
        Dispatcher.Invoke(() => { });
    }

    private void LoadDocumento()
    {
        if (_documento == null) return;

        TxtNumero.Text = _documento.NumeroDocumento;
        DpData.SelectedDate = _documento.DataDocumento;

        _suppressPartySelection = true;
        if (_documento.ClienteId.HasValue)
            CmbCliente.SelectedValue = _documento.ClienteId.Value;
        if (_documento.FornitoreId.HasValue)
            CmbFornitore.SelectedValue = _documento.FornitoreId.Value;
        _suppressPartySelection = false;

        if (CmbMagazzino.ItemsSource != null)
            CmbMagazzino.SelectedValue = _documento.MagazzinoId;

        TxtImponibile.Text = _documento.Imponibile.ToString(CultureInfo.CurrentCulture);
        TxtIva.Text = _documento.IVA.ToString(CultureInfo.CurrentCulture);
        TxtTotale.Text = _documento.Totale.ToString(CultureInfo.CurrentCulture);
        TxtSconto.Text = _documento.ScontoGlobale.ToString(CultureInfo.CurrentCulture);
        TxtSpese.Text = _documento.SpeseAccessorie.ToString(CultureInfo.CurrentCulture);

        TxtPagamento.Text = _documento.ModalitaPagamento ?? string.Empty;
        TxtBanca.Text = _documento.BancaAppoggio ?? string.Empty;
        DpScadenza.SelectedDate = _documento.DataScadenzaPagamento;

        // Precompila scadenza se mancante (non salva automaticamente: viene salvata solo con Salva)
        TryAutoSetScadenzaPagamentoFromCliente();

        ChkPagato.IsChecked = _documento.Pagato;
        DpDataPagamento.SelectedDate = _documento.DataPagamento;

        // Data pagamento solo per fatture
        var isFattura = IsFatturaTipo(_tipoDocumento);
        DpDataPagamento.IsEnabled = isFattura;
        DpDataPagamento.Visibility = isFattura ? Visibility.Visible : Visibility.Collapsed;
        ChkFE.IsChecked = _documento.FatturaElettronica;
        TxtCodiceSDI.Text = _documento.CodiceSDI ?? string.Empty;
        TxtPEC.Text = _documento.PECDestinatario ?? string.Empty;

        TxtCausale.Text = _documento.CausaleDocumento ?? string.Empty;
        TxtAspetto.Text = _documento.AspettoBeni ?? string.Empty;
        TxtTrasporto.Text = _documento.TrasportoCura ?? string.Empty;
        TxtVettore.Text = _documento.Vettore ?? string.Empty;

        TxtNote.Text = _documento.Note ?? string.Empty;
    }

    private void ChkPagato_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsFatturaTipo(_tipoDocumento)) return;
        if (DpDataPagamento.SelectedDate is null)
            DpDataPagamento.SelectedDate = DateTime.Today;
    }

    private void ChkPagato_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!IsFatturaTipo(_tipoDocumento)) return;
        DpDataPagamento.SelectedDate = null;
    }

    private void DpDataPagamento_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsFatturaTipo(_tipoDocumento)) return;
        if (DpDataPagamento.SelectedDate is not null && ChkPagato.IsChecked != true)
            ChkPagato.IsChecked = true;
    }

    private void LoadRighe()
    {
        _righe.Clear();
        var rows = (_documento?.Righe ?? Array.Empty<DocumentoRiga>())
            .OrderBy(r => r.NumeroRiga)
            .ToList();

        foreach (var r in rows)
        {
            _righe.Add(new DocumentoRigaVm
            {
                Id = r.Id,
                NumeroRiga = r.NumeroRiga,
                ArticoloId = r.ArticoloId,
                Descrizione = r.Descrizione,
                Quantita = r.Quantita,
                PrezzoUnitario = r.PrezzoUnitario,
                AliquotaIVA = r.AliquotaIVA,
                UnitaMisura = r.UnitaMisura,
                NumeroSerie = r.NumeroSerie,
                Lotto = r.Lotto,
                RigaDescrittiva = r.RigaDescrittiva,
                Note = r.Note
            });
        }

        RefreshDerivedFields();
        RecalculateTotalsFromRighe(updateTextBoxes: false);
    }

    private void RefreshDerivedFields()
    {
        foreach (var r in _righe)
        {
            r.ArticoloCodice = r.ArticoloId.HasValue
                ? _articoli.FirstOrDefault(a => a.Id == r.ArticoloId.Value)?.Codice
                : null;

            if (r.ArticoloId.HasValue)
            {
                var art = _articoli.FirstOrDefault(a => a.Id == r.ArticoloId.Value);
                if (art != null)
                {
                    if (string.IsNullOrWhiteSpace(r.Descrizione)) r.Descrizione = art.Descrizione;
                    if (r.AliquotaIVA <= 0) r.AliquotaIVA = art.AliquotaIVA;
                    if (r.PrezzoUnitario <= 0) r.PrezzoUnitario = art.PrezzoVendita;
                    if (string.IsNullOrWhiteSpace(r.UnitaMisura)) r.UnitaMisura = art.UnitaMisura;
                }
            }

            r.Recalc();
        }

        RigheDataGrid.Items.Refresh();
    }

    private void RecalculateTotalsFromRighe(bool updateTextBoxes)
    {
        foreach (var r in _righe) r.Recalc();

        var imponibile = _righe.Sum(r => r.Imponibile);
        var iva = _righe.Sum(r => r.ImportoIVA);
        var totale = _righe.Sum(r => r.Totale);

        if (!TryParseDecimal(TxtSconto.Text, out var sconto)) sconto = 0m;
        if (!TryParseDecimal(TxtSpese.Text, out var spese)) spese = 0m;

        // Semplificazione: sconto/spese applicati come valori assoluti ai totali.
        imponibile = imponibile - sconto + spese;
        totale = totale - sconto + spese;

        if (updateTextBoxes)
        {
            TxtImponibile.Text = imponibile.ToString(CultureInfo.CurrentCulture);
            TxtIva.Text = iva.ToString(CultureInfo.CurrentCulture);
            TxtTotale.Text = totale.ToString(CultureInfo.CurrentCulture);
        }
    }

    private void BtnAggiungiRiga_Click(object sender, RoutedEventArgs e)
    {
        var nextNum = _righe.Count == 0 ? 1 : _righe.Max(r => r.NumeroRiga) + 1;
        _righe.Add(new DocumentoRigaVm { NumeroRiga = nextNum, Quantita = 1m });
        RigheDataGrid.SelectedIndex = _righe.Count - 1;
    }

    private void BtnRimuoviRiga_Click(object sender, RoutedEventArgs e)
    {
        if (RigheDataGrid.SelectedItem is not DocumentoRigaVm vm)
        {
            MessageBox.Show("Seleziona una riga da rimuovere.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _righe.Remove(vm);
        // rinumera
        var i = 1;
        foreach (var r in _righe.OrderBy(r => r.NumeroRiga)) r.NumeroRiga = i++;
        RefreshDerivedFields();
    }

    private async Task<string> GeneraNuovoNumero()
    {
        return await DocumentNumberService.GenerateNumeroDocumentoAsync(
            _context,
            _tipoDocumento,
            DateTime.Today);
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnStampa_Click(object sender, RoutedEventArgs e)
    {
        if (!_documentoId.HasValue)
        {
            MessageBox.Show("Salva il documento prima di stampare.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var doc = await _context.Documenti
                .AsNoTracking()
                .Include(d => d.Cliente)
                .Include(d => d.Fornitore)
                .Include("Righe.Articolo")
                .FirstOrDefaultAsync(d => d.Id == _documentoId.Value);

            if (doc == null)
            {
                MessageBox.Show("Documento non trovato.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var righe = (doc.Righe ?? Array.Empty<DocumentoRiga>())
                .OrderBy(r => r.NumeroRiga)
                .ToList();

            var settings = new AppSettingsService().Load();
            DocumentoPrintService.PrintDocumento(this, doc, righe, settings);
        }
        catch (Exception ex)
        {
            UiLog.Error("DocumentoEditWindow.BtnStampa_Click", ex);
            MessageBox.Show($"Errore stampa: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNumero.Text))
        {
            MessageBox.Show("Numero documento obbligatorio.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var numero = TxtNumero.Text.Trim();

        var existingId = await _context.Documenti
            .AsNoTracking()
            .Where(d => d.TipoDocumento == _tipoDocumento && d.NumeroDocumento == numero)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        if (existingId != 0 && (_documento == null || existingId != _documento.Id))
        {
            MessageBox.Show(
                "Esiste già un documento con lo stesso numero per questo tipo.\n\n" +
                $"Tipo: {_tipoDocumento}\nNumero: {numero}",
                "Attenzione",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (DpData.SelectedDate is null)
        {
            MessageBox.Show("Seleziona la data documento.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var clienteId = CmbCliente.SelectedValue as int?;
        var fornitoreId = CmbFornitore.SelectedValue as int?;
        var magazzinoId = CmbMagazzino.SelectedValue as int?;
        if (!magazzinoId.HasValue) magazzinoId = 1;

        if (RequiresClienteOnly(_tipoDocumento))
        {
            if (!clienteId.HasValue)
            {
                MessageBox.Show("Seleziona un cliente.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            fornitoreId = null;
        }
        else
        {
            if (!clienteId.HasValue && !fornitoreId.HasValue)
            {
                MessageBox.Show("Seleziona un cliente o un fornitore.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Selezione mutualmente esclusiva (per sicurezza)
            if (clienteId.HasValue && fornitoreId.HasValue)
            {
                MessageBox.Show("Seleziona solo cliente oppure solo fornitore.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (!TryParseDecimal(TxtSconto.Text, out var sconto) ||
            !TryParseDecimal(TxtSpese.Text, out var spese))
        {
            MessageBox.Show("Verifica i valori numerici (sconto/spese).", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_documento == null)
            {
                _documento = new Documento();
                _context.Documenti.Add(_documento);
            }

            _documento.TipoDocumento = _tipoDocumento;
            _documento.NumeroDocumento = numero;
            _documento.DataDocumento = DpData.SelectedDate.Value;
            _documento.ClienteId = clienteId;
            _documento.FornitoreId = fornitoreId;
            _documento.MagazzinoId = magazzinoId.Value;

            _documento.ScontoGlobale = sconto;
            _documento.SpeseAccessorie = spese;

            _documento.ModalitaPagamento = EmptyToNull(TxtPagamento.Text);
            _documento.BancaAppoggio = EmptyToNull(TxtBanca.Text);
            if (DpScadenza.SelectedDate is null)
                TryAutoSetScadenzaPagamentoFromCliente();

            _documento.DataScadenzaPagamento = DpScadenza.SelectedDate;

            _documento.Pagato = ChkPagato.IsChecked == true;
            if (IsFatturaTipo(_tipoDocumento))
            {
                if (_documento.Pagato)
                    _documento.DataPagamento = (DpDataPagamento.SelectedDate ?? DateTime.Today).Date;
                else
                    _documento.DataPagamento = null;
            }
            else
            {
                _documento.DataPagamento = null;
            }
            _documento.FatturaElettronica = ChkFE.IsChecked == true;
            _documento.CodiceSDI = EmptyToNull(TxtCodiceSDI.Text);
            _documento.PECDestinatario = EmptyToNull(TxtPEC.Text);

            _documento.CausaleDocumento = EmptyToNull(TxtCausale.Text);
            _documento.AspettoBeni = EmptyToNull(TxtAspetto.Text);
            _documento.TrasportoCura = EmptyToNull(TxtTrasporto.Text);
            _documento.Vettore = EmptyToNull(TxtVettore.Text);

            _documento.Note = EmptyToNull(TxtNote.Text);
            _documento.DataModifica = DateTime.Now;

            // Aggiorna campi derivati dalle righe e prepara le entità riga
            RefreshDerivedFields();
            RecalculateTotalsFromRighe(updateTextBoxes: true);

            // Validazioni minime righe
            foreach (var r in _righe)
            {
                if (r.RigaDescrittiva) continue;
                if (r.ArticoloId.HasValue && r.Quantita <= 0)
                {
                    MessageBox.Show("Le quantità di riga devono essere maggiori di zero.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            TryParseDecimal(TxtImponibile.Text, out var imponibileFinale);
            TryParseDecimal(TxtIva.Text, out var ivaFinale);
            TryParseDecimal(TxtTotale.Text, out var totaleFinale);
            _documento.Imponibile = imponibileFinale;
            _documento.IVA = ivaFinale;
            _documento.Totale = totaleFinale;

            var righePersist = _righe
                .OrderBy(r => r.NumeroRiga)
                .Select(r => r.ToEntity())
                .ToList();

            // Salva testata per ottenere Id su inserimento
            await _context.SaveChangesAsync();

            // Sync righe
            var existing = await _context.DocumentiRighe
                .Where(r => r.DocumentoId == _documento.Id)
                .ToListAsync();

            var keepIds = new HashSet<int>(righePersist.Where(r => r.Id != 0).Select(r => r.Id));
            foreach (var old in existing)
            {
                if (!keepIds.Contains(old.Id))
                {
                    _context.DocumentiRighe.Remove(old);
                }
            }

            foreach (var r in righePersist)
            {
                r.DocumentoId = _documento.Id;
                if (r.Id == 0)
                {
                    _context.DocumentiRighe.Add(r);
                }
                else
                {
                    var target = existing.FirstOrDefault(x => x.Id == r.Id);
                    if (target == null)
                    {
                        // riga presente in memoria ma non nel db (caso raro): inserisci come nuova
                        r.Id = 0;
                        _context.DocumentiRighe.Add(r);
                    }
                    else
                    {
                        target.NumeroRiga = r.NumeroRiga;
                        target.ArticoloId = r.ArticoloId;
                        target.Descrizione = r.Descrizione;
                        target.Quantita = r.Quantita;
                        target.UnitaMisura = r.UnitaMisura;
                        target.PrezzoUnitario = r.PrezzoUnitario;
                        target.Sconto1 = r.Sconto1;
                        target.Sconto2 = r.Sconto2;
                        target.Sconto3 = r.Sconto3;
                        target.PrezzoNetto = r.PrezzoNetto;
                        target.AliquotaIVA = r.AliquotaIVA;
                        target.Imponibile = r.Imponibile;
                        target.ImportoIVA = r.ImportoIVA;
                        target.Totale = r.Totale;
                        target.NumeroSerie = r.NumeroSerie;
                        target.Lotto = r.Lotto;
                        target.RigaDescrittiva = r.RigaDescrittiva;
                        target.Note = r.Note;
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Movimenti magazzino (MVP): genera da documenti che movimentano
            var magazzinoService = new DocumentoMagazzinoService(_context);
            await magazzinoService.SyncMovimentiMagazzinoForDocumentoAsync(
                _documento.Id,
                _documento.TipoDocumento,
                _documento.NumeroDocumento,
                _documento.DataDocumento,
                _documento.DocumentoOriginaleId);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("DocumentoEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TryAutoSetScadenzaPagamentoFromCliente()
    {
        if (!IsFatturaTipo(_tipoDocumento)) return;
        if (DpData.SelectedDate is null) return;
        if (DpScadenza.SelectedDate is not null) return;
        if (ChkPagato.IsChecked == true) return;

        if (CmbCliente.SelectedItem is not Cliente c) return;

        var giorni = c.GiorniPagamento ?? 30;
        if (giorni < 0) giorni = 0;

        DpScadenza.SelectedDate = DpData.SelectedDate.Value.AddDays(giorni);
    }

    private static bool IsFatturaTipo(string tipoDocumento)
    {
        var t = (tipoDocumento ?? string.Empty).ToUpperInvariant();
        return t.Contains("FATTURA");
    }

    private static bool RequiresClienteOnly(string tipoDocumento)
    {
        var t = (tipoDocumento ?? string.Empty).ToUpperInvariant();
        return t == "PREVENTIVO";
    }

    private sealed class ComboItem
    {
        public int Id { get; set; }
        public string Display { get; set; } = string.Empty;
        public override string ToString() => Display;
    }

    private sealed class DocumentoRigaVm
    {
        public int Id { get; set; }
        public int NumeroRiga { get; set; }
        public int? ArticoloId { get; set; }
        public string? ArticoloCodice { get; set; }
        public string Descrizione { get; set; } = string.Empty;
        public decimal Quantita { get; set; }
        public decimal PrezzoUnitario { get; set; }
        public decimal AliquotaIVA { get; set; }
        public string? UnitaMisura { get; set; }
        public string? NumeroSerie { get; set; }
        public string? Lotto { get; set; }
        public bool RigaDescrittiva { get; set; }
        public string? Note { get; set; }

        public decimal PrezzoNetto { get; private set; }
        public decimal Imponibile { get; private set; }
        public decimal ImportoIVA { get; private set; }
        public decimal Totale { get; private set; }

        public void Recalc()
        {
            // Semplificazione: niente sconti riga nella UI minimale.
            PrezzoNetto = PrezzoUnitario;
            Imponibile = Math.Round(Quantita * PrezzoNetto, 2);
            ImportoIVA = Math.Round(Imponibile * (AliquotaIVA / 100m), 2);
            Totale = Imponibile + ImportoIVA;
        }

        public DocumentoRiga ToEntity()
        {
            Recalc();
            return new DocumentoRiga
            {
                Id = Id,
                NumeroRiga = NumeroRiga,
                ArticoloId = ArticoloId,
                Descrizione = (Descrizione ?? string.Empty).Trim(),
                Quantita = Quantita,
                UnitaMisura = string.IsNullOrWhiteSpace(UnitaMisura) ? null : UnitaMisura.Trim(),
                PrezzoUnitario = PrezzoUnitario,
                Sconto1 = 0m,
                Sconto2 = 0m,
                Sconto3 = 0m,
                PrezzoNetto = PrezzoNetto,
                AliquotaIVA = AliquotaIVA,
                Imponibile = Imponibile,
                ImportoIVA = ImportoIVA,
                Totale = Totale,
                NumeroSerie = string.IsNullOrWhiteSpace(NumeroSerie) ? null : NumeroSerie.Trim(),
                Lotto = string.IsNullOrWhiteSpace(Lotto) ? null : Lotto.Trim(),
                RigaDescrittiva = RigaDescrittiva,
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
            };
        }
    }

    private static string? EmptyToNull(string? s)
    {
        s = (s ?? string.Empty).Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
        {
            value = 0m;
            return true;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
