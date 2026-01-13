using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.Data.Services;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;
using DidoGest.UI.Windows;

namespace DidoGest.UI.Views.Documenti;

public partial class OrdiniView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Ordine> _all = new();

    public OrdiniView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();

        Loaded += OrdiniView_Loaded;
    }

    private async void OrdiniView_Loaded(object sender, RoutedEventArgs e)
    {
        CmbTipo.ItemsSource = new[] { "TUTTI", "CLIENTE", "FORNITORE" };
        CmbTipo.SelectedIndex = 0;
        await LoadOrdini();
    }

    private async Task LoadOrdini()
    {
        try
        {
            var tipo = CmbTipo.SelectedItem as string;

            var query = _context.Ordini
                .Include(o => o.Cliente)
                .Include(o => o.Fornitore)
                .AsQueryable();

            if (tipo == "CLIENTE" || tipo == "FORNITORE")
            {
                query = query.Where(o => o.TipoOrdine == tipo);
            }

            _all = await query.OrderByDescending(o => o.DataOrdine).ToListAsync();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            UiLog.Error("OrdiniView.LoadOrdini", ex);
            MessageBox.Show($"Errore caricamento ordini: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        var s = (SearchBox.Text ?? string.Empty).ToLower();
        var filtered = string.IsNullOrWhiteSpace(s)
            ? _all
            : _all.Where(o =>
                o.NumeroOrdine.ToLower().Contains(s) ||
                o.TipoOrdine.ToLower().Contains(s) ||
                (o.Cliente != null && o.Cliente.RagioneSociale.ToLower().Contains(s)) ||
                (o.Fornitore != null && o.Fornitore.RagioneSociale.ToLower().Contains(s)) ||
                (o.StatoOrdine != null && o.StatoOrdine.ToLower().Contains(s))
            ).ToList();

        OrdiniDataGrid.ItemsSource = filtered;
        Totale.Text = filtered.Count.ToString();
    }

    private async void CmbTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadOrdini();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Windows.OrdineEditWindow(_context);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadOrdini();
        }
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (OrdiniDataGrid.SelectedItem is not Ordine ordine)
        {
            MessageBox.Show("Seleziona un ordine", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Windows.OrdineEditWindow(_context, ordine.Id);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadOrdini();
        }
    }

    private void OrdiniDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (OrdiniDataGrid.SelectedItem is not Ordine ordine)
        {
            MessageBox.Show("Seleziona un ordine", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show("Eliminare l'ordine selezionato?", "Conferma", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            _context.Ordini.Remove(ordine);
            await _context.SaveChangesAsync();
            await LoadOrdini();
        }
        catch (Exception ex)
        {
            UiLog.Error("OrdiniView.BtnElimina_Click", ex);
            MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnCreaDDT_Click(object sender, RoutedEventArgs e)
    {
        if (OrdiniDataGrid.SelectedItem is not Ordine ordineSel)
        {
            MessageBox.Show("Seleziona un ordine", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var ordine = await _context.Ordini
                .Include(o => o.Cliente)
                .Include(o => o.Fornitore)
                .Include(o => o.Righe)
                .FirstOrDefaultAsync(o => o.Id == ordineSel.Id);

            if (ordine == null)
            {
                MessageBox.Show("Ordine non trovato.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ordine.TipoOrdine == "CLIENTE")
            {
                if (!ordine.ClienteId.HasValue)
                {
                    MessageBox.Show("L'ordine non ha un cliente associato.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else if (ordine.TipoOrdine == "FORNITORE")
            {
                if (!ordine.FornitoreId.HasValue)
                {
                    MessageBox.Show("L'ordine non ha un fornitore associato.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                MessageBox.Show("Tipo ordine non supportato.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var righe = (ordine.Righe ?? Array.Empty<OrdineRiga>())
                .Where(r => r.ArticoloId.HasValue && r.QuantitaResiduata > 0)
                .OrderBy(r => r.NumeroRiga)
                .ToList();

            if (righe.Count == 0)
            {
                MessageBox.Show("L'ordine non ha righe residue da evadere.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show(
                $"Creare un DDT dall'ordine {ordine.NumeroOrdine}?\n\nVerranno copiate le righe residue.",
                "Conferma",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            // Crea testata documento
            var numeroDDT = await DocumentNumberService.GenerateNumeroDocumentoAsync(
                _context,
                "DDT",
                DateTime.Today);

            var magazzinoIdDefault = await _context.Magazzini
                .AsNoTracking()
                .OrderByDescending(m => m.Principale)
                .ThenBy(m => m.Id)
                .Select(m => m.Id)
                .FirstOrDefaultAsync();
            if (magazzinoIdDefault == 0) magazzinoIdDefault = 1;

            var doc = new Documento
            {
                TipoDocumento = "DDT",
                NumeroDocumento = numeroDDT,
                DataDocumento = DateTime.Today,
                MagazzinoId = magazzinoIdDefault,
                ClienteId = ordine.TipoOrdine == "CLIENTE" ? ordine.ClienteId : null,
                FornitoreId = ordine.TipoOrdine == "FORNITORE" ? ordine.FornitoreId : null,
                RagioneSocialeDestinatario = ordine.TipoOrdine == "CLIENTE"
                    ? ordine.Cliente?.RagioneSociale
                    : ordine.Fornitore?.RagioneSociale,
                IndirizzoDestinatario = ordine.TipoOrdine == "CLIENTE"
                    ? ordine.Cliente?.Indirizzo
                    : ordine.Fornitore?.Indirizzo,
                Imponibile = 0m,
                IVA = 0m,
                Totale = 0m,
                ScontoGlobale = 0m,
                SpeseAccessorie = 0m,
                Note = $"Generato da Ordine {ordine.NumeroOrdine}"
            };
            _context.Documenti.Add(doc);
            await _context.SaveChangesAsync();

            // Crea righe documento
            var imponibile = 0m;
            var iva = 0m;
            var totale = 0m;
            var i = 1;

            foreach (var r in righe)
            {
                var qta = r.QuantitaResiduata;
                var imponibileRiga = Math.Round(qta * r.PrezzoUnitario, 2);
                var ivaRiga = Math.Round(imponibileRiga * (r.AliquotaIVA / 100m), 2);
                var totaleRiga = imponibileRiga + ivaRiga;

                imponibile += imponibileRiga;
                iva += ivaRiga;
                totale += totaleRiga;

                _context.DocumentiRighe.Add(new DocumentoRiga
                {
                    DocumentoId = doc.Id,
                    NumeroRiga = i++,
                    ArticoloId = r.ArticoloId,
                    Descrizione = r.Descrizione,
                    Quantita = qta,
                    UnitaMisura = r.UnitaMisura,
                    PrezzoUnitario = r.PrezzoUnitario,
                    Sconto1 = 0m,
                    Sconto2 = 0m,
                    Sconto3 = 0m,
                    PrezzoNetto = r.PrezzoUnitario,
                    AliquotaIVA = r.AliquotaIVA,
                    Imponibile = imponibileRiga,
                    ImportoIVA = ivaRiga,
                    Totale = totaleRiga,
                    RigaDescrittiva = false
                });

                // Evasione: incrementa di quanto copiato nel DDT
                r.QuantitaEvasa += qta;
            }

            doc.Imponibile = imponibile;
            doc.IVA = iva;
            doc.Totale = totale;

            ordine.StatoOrdine = (ordine.Righe ?? Array.Empty<OrdineRiga>()).Any(x => x.QuantitaResiduata > 0)
                ? "PARZIALMENTE_EVASO"
                : "EVASO";
            ordine.DataModifica = DateTime.Now;

            await _context.SaveChangesAsync();

            // Genera subito movimenti/giacenze per il DDT appena creato
            var magSvc = new DocumentoMagazzinoService(_context);
            await magSvc.SyncMovimentiMagazzinoForDocumentoAsync(
                doc.Id,
                doc.TipoDocumento,
                doc.NumeroDocumento,
                doc.DataDocumento,
                doc.DocumentoOriginaleId);

            MessageBox.Show("DDT creato dall'ordine selezionato.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            var w = new DocumentoEditWindow(_context, "DDT", doc.Id);
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            UiLog.Error("OrdiniView.BtnCreaDDT_Click", ex);
            MessageBox.Show($"Errore creazione DDT: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            await LoadOrdini();
        }
    }
}
