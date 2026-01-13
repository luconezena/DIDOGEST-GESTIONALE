using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class OrdineEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _ordineId;
    private Ordine? _ordine;

    private List<Articolo> _articoli = new();
    private readonly ObservableCollection<OrdineRigaVm> _righe = new();

    public IEnumerable<Articolo> ArticoliForCombo => _articoli;

    public OrdineEditWindow(DidoGestDbContext context, int? ordineId = null)
    {
        InitializeComponent();
        _context = context;
        _ordineId = ordineId;

        Loaded += OrdineEditWindow_Loaded;
    }

    private async void OrdineEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CmbTipo.ItemsSource = new[] { "CLIENTE", "FORNITORE" };
        CmbTipo.SelectedIndex = 0;

        var clienti = await _context.Clienti.OrderBy(c => c.RagioneSociale).ToListAsync();
        CmbCliente.ItemsSource = clienti;
        CmbCliente.DisplayMemberPath = "RagioneSociale";

        var fornitori = await _context.Fornitori.OrderBy(f => f.RagioneSociale).ToListAsync();
        CmbFornitore.ItemsSource = fornitori;
        CmbFornitore.DisplayMemberPath = "RagioneSociale";

        _articoli = await _context.Articoli.AsNoTracking().OrderBy(a => a.Codice).ToListAsync();
        // serve per il binding del ComboBox nel DataGrid
        Dispatcher.Invoke(() => { });

        RigheDataGrid.ItemsSource = _righe;

        if (_ordineId.HasValue)
        {
            _ordine = await _context.Ordini
                .Include(o => o.Cliente)
                .Include(o => o.Fornitore)
                .Include(o => o.Righe)
                .FirstOrDefaultAsync(o => o.Id == _ordineId.Value);
            if (_ordine != null)
            {
                Title = "Modifica Ordine";
                CmbTipo.SelectedItem = _ordine.TipoOrdine;
                TxtNumero.Text = _ordine.NumeroOrdine;
                DpData.SelectedDate = _ordine.DataOrdine;
                TxtStato.Text = _ordine.StatoOrdine;
                TxtNote.Text = _ordine.Note;

                if (_ordine.ClienteId.HasValue)
                {
                    CmbCliente.SelectedItem = clienti.FirstOrDefault(c => c.Id == _ordine.ClienteId.Value);
                }
                if (_ordine.FornitoreId.HasValue)
                {
                    CmbFornitore.SelectedItem = fornitori.FirstOrDefault(f => f.Id == _ordine.FornitoreId.Value);
                }

                ApplyTipo();

                LoadRighe();
                return;
            }
        }

        Title = "Nuovo Ordine";
        DpData.SelectedDate = DateTime.Today;
        TxtStato.Text = "APERTO";
        ApplyTipo();
        TxtNumero.Text = await GeneraNumero();
        CmbCliente.SelectedIndex = clienti.Count > 0 ? 0 : -1;
        CmbFornitore.SelectedIndex = fornitori.Count > 0 ? 0 : -1;

        _righe.Clear();
        RefreshDerivedFields();
    }

    private void LoadRighe()
    {
        _righe.Clear();
        var rows = (_ordine?.Righe ?? Array.Empty<OrdineRiga>())
            .OrderBy(r => r.NumeroRiga)
            .ToList();

        foreach (var r in rows)
        {
            _righe.Add(new OrdineRigaVm
            {
                Id = r.Id,
                NumeroRiga = r.NumeroRiga,
                ArticoloId = r.ArticoloId,
                Descrizione = r.Descrizione,
                QuantitaOrdinata = r.QuantitaOrdinata,
                PrezzoUnitario = r.PrezzoUnitario,
                AliquotaIVA = r.AliquotaIVA,
                UnitaMisura = r.UnitaMisura
            });
        }

        RefreshDerivedFields();
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

        var imponibile = _righe.Sum(x => x.Imponibile);
        var iva = _righe.Sum(x => x.ImportoIVA);
        var totale = _righe.Sum(x => x.Totale);

        LblImponibile.Text = imponibile.ToString("N2", CultureInfo.CurrentCulture);
        LblIva.Text = iva.ToString("N2", CultureInfo.CurrentCulture);
        LblTotale.Text = totale.ToString("N2", CultureInfo.CurrentCulture);

        RigheDataGrid.Items.Refresh();
    }

    private void BtnAggiungiRiga_Click(object sender, RoutedEventArgs e)
    {
        var nextNum = _righe.Count == 0 ? 1 : _righe.Max(r => r.NumeroRiga) + 1;
        _righe.Add(new OrdineRigaVm { NumeroRiga = nextNum, QuantitaOrdinata = 1m });
        RigheDataGrid.SelectedIndex = _righe.Count - 1;
        RefreshDerivedFields();
    }

    private void BtnRimuoviRiga_Click(object sender, RoutedEventArgs e)
    {
        if (RigheDataGrid.SelectedItem is not OrdineRigaVm vm)
        {
            MessageBox.Show("Seleziona una riga da rimuovere.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _righe.Remove(vm);
        var i = 1;
        foreach (var r in _righe.OrderBy(r => r.NumeroRiga)) r.NumeroRiga = i++;
        RefreshDerivedFields();
    }

    private void CmbTipo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyTipo();
    }

    private void ApplyTipo()
    {
        var tipo = (CmbTipo.SelectedItem as string) ?? "CLIENTE";
        CmbCliente.IsEnabled = tipo == "CLIENTE";
        CmbFornitore.IsEnabled = tipo == "FORNITORE";
    }

    private async Task<string> GeneraNumero()
    {
        var tipo = (CmbTipo.SelectedItem as string) ?? "CLIENTE";
        var prefix = tipo == "CLIENTE" ? "OC" : "OF";

        var last = await _context.Ordini
            .Where(o => o.TipoOrdine == tipo)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        int numero = 1;
        if (last != null)
        {
            var digits = new string(last.NumeroOrdine.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int n)) numero = n + 1;
        }

        return $"{prefix}{numero:D6}";
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        var tipo = (CmbTipo.SelectedItem as string) ?? "CLIENTE";

        if (tipo == "CLIENTE" && CmbCliente.SelectedItem is not Cliente)
        {
            MessageBox.Show("Seleziona un cliente", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (tipo == "FORNITORE" && CmbFornitore.SelectedItem is not Fornitore)
        {
            MessageBox.Show("Seleziona un fornitore", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_ordine == null)
            {
                _ordine = new Ordine
                {
                    TipoOrdine = tipo,
                    Imponibile = 0m,
                    IVA = 0m,
                    Totale = 0m
                };
                _context.Ordini.Add(_ordine);
            }

            _ordine.TipoOrdine = tipo;
            _ordine.NumeroOrdine = (TxtNumero.Text ?? string.Empty).Trim();
            _ordine.DataOrdine = DpData.SelectedDate ?? DateTime.Today;
            _ordine.StatoOrdine = TxtStato.Text?.Trim();
            _ordine.Note = TxtNote.Text?.Trim();

            if (tipo == "CLIENTE")
            {
                _ordine.ClienteId = ((Cliente)CmbCliente.SelectedItem).Id;
                _ordine.FornitoreId = null;
            }
            else
            {
                _ordine.FornitoreId = ((Fornitore)CmbFornitore.SelectedItem).Id;
                _ordine.ClienteId = null;
            }

            // Validazioni righe (minime)
            RefreshDerivedFields();
            foreach (var r in _righe)
            {
                if (!r.ArticoloId.HasValue)
                {
                    MessageBox.Show("Ogni riga deve avere un articolo.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (r.QuantitaOrdinata <= 0)
                {
                    MessageBox.Show("Le quantità ordinate devono essere maggiori di zero.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Totali ordine dalle righe
            _ordine.Imponibile = _righe.Sum(x => x.Imponibile);
            _ordine.IVA = _righe.Sum(x => x.ImportoIVA);
            _ordine.Totale = _righe.Sum(x => x.Totale);
            _ordine.DataModifica = DateTime.Now;

            // Salva testata per ottenere Id
            await _context.SaveChangesAsync();

            // Sync righe ordine
            var righePersist = _righe
                .OrderBy(r => r.NumeroRiga)
                .Select(r => r.ToEntity())
                .ToList();

            var existing = await _context.OrdiniRighe
                .Where(r => r.OrdineId == _ordine.Id)
                .ToListAsync();

            var keepIds = new HashSet<int>(righePersist.Where(r => r.Id != 0).Select(r => r.Id));
            foreach (var old in existing)
            {
                if (!keepIds.Contains(old.Id))
                    _context.OrdiniRighe.Remove(old);
            }

            foreach (var r in righePersist)
            {
                r.OrdineId = _ordine.Id;
                if (r.Id == 0)
                {
                    _context.OrdiniRighe.Add(r);
                }
                else
                {
                    var target = existing.FirstOrDefault(x => x.Id == r.Id);
                    if (target == null)
                    {
                        r.Id = 0;
                        _context.OrdiniRighe.Add(r);
                    }
                    else
                    {
                        target.NumeroRiga = r.NumeroRiga;
                        target.ArticoloId = r.ArticoloId;
                        target.Descrizione = r.Descrizione;
                        target.QuantitaOrdinata = r.QuantitaOrdinata;
                        // QuantitaEvasa gestita dal flusso evasione (es. DDT)
                        target.UnitaMisura = r.UnitaMisura;
                        target.PrezzoUnitario = r.PrezzoUnitario;
                        target.Sconto = r.Sconto;
                        target.AliquotaIVA = r.AliquotaIVA;
                        target.Totale = r.Totale;
                        target.Note = r.Note;
                    }
                }
            }

            await _context.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("OrdineEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed class OrdineRigaVm
    {
        public int Id { get; set; }
        public int NumeroRiga { get; set; }
        public int? ArticoloId { get; set; }
        public string? ArticoloCodice { get; set; }
        public string Descrizione { get; set; } = string.Empty;
        public decimal QuantitaOrdinata { get; set; }
        public decimal PrezzoUnitario { get; set; }
        public decimal AliquotaIVA { get; set; }
        public string? UnitaMisura { get; set; }
        public string? Note { get; set; }

        public decimal Imponibile { get; private set; }
        public decimal ImportoIVA { get; private set; }
        public decimal Totale { get; private set; }

        public void Recalc()
        {
            Imponibile = Math.Round(QuantitaOrdinata * PrezzoUnitario, 2);
            ImportoIVA = Math.Round(Imponibile * (AliquotaIVA / 100m), 2);
            Totale = Imponibile + ImportoIVA;
        }

        public OrdineRiga ToEntity()
        {
            Recalc();
            return new OrdineRiga
            {
                Id = Id,
                NumeroRiga = NumeroRiga,
                ArticoloId = ArticoloId,
                Descrizione = (Descrizione ?? string.Empty).Trim(),
                QuantitaOrdinata = QuantitaOrdinata,
                // QuantitaEvasa gestita separatamente
                QuantitaEvasa = 0m,
                UnitaMisura = string.IsNullOrWhiteSpace(UnitaMisura) ? null : UnitaMisura.Trim(),
                PrezzoUnitario = PrezzoUnitario,
                Sconto = 0m,
                AliquotaIVA = AliquotaIVA,
                Totale = Totale,
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
            };
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
