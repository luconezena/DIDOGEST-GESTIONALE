using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;
using DidoGest.UI.Windows;

namespace DidoGest.UI.Views.Documenti;

public partial class PreventiviView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Documento> _all = new();

    public PreventiviView()
    {
        InitializeComponent();
            _context = DidoGestDb.CreateContext();

        Loaded += PreventiviView_Loaded;
    }

    private async void PreventiviView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadPreventivi();
    }

    private async Task LoadPreventivi()
    {
        try
        {
            _all = await _context.Documenti
                .Include(d => d.Cliente)
                .Where(d => d.TipoDocumento == "PREVENTIVO")
                .OrderByDescending(d => d.DataDocumento)
                .ToListAsync();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            UiLog.Error("PreventiviView.LoadPreventivi", ex);
            MessageBox.Show($"Errore caricamento preventivi: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        var s = (SearchBox.Text ?? string.Empty).ToLower();
        var filtered = string.IsNullOrWhiteSpace(s)
            ? _all
            : _all.Where(d =>
                d.NumeroDocumento.ToLower().Contains(s) ||
                (d.Cliente != null && d.Cliente.RagioneSociale.ToLower().Contains(s)) ||
                (d.Note != null && d.Note.ToLower().Contains(s))
            ).ToList();

        PreventiviDataGrid.ItemsSource = filtered;
        Totale.Text = filtered.Count.ToString();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new DocumentoEditWindow(_context, "PREVENTIVO", null);
        if (w.ShowDialog() == true)
            _ = LoadPreventivi();
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (PreventiviDataGrid.SelectedItem is not Documento doc)
        {
            MessageBox.Show("Seleziona un preventivo", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var w = new DocumentoEditWindow(_context, "PREVENTIVO", doc.Id);
        if (w.ShowDialog() == true)
            _ = LoadPreventivi();
    }

    private void PreventiviDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnConvertiInOrdine_Click(object sender, RoutedEventArgs e)
    {
        if (PreventiviDataGrid.SelectedItem is not Documento doc)
        {
            MessageBox.Show("Seleziona un preventivo", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var preventivo = await _context.Documenti
                .AsNoTracking()
                .Include(d => d.Righe)
                .FirstOrDefaultAsync(d => d.Id == doc.Id);

            if (preventivo == null)
            {
                MessageBox.Show("Preventivo non trovato.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!preventivo.ClienteId.HasValue)
            {
                MessageBox.Show("Il preventivo non ha un cliente associato.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var righe = (preventivo.Righe ?? Array.Empty<DocumentoRiga>())
                .Where(r => r.ArticoloId.HasValue && !r.RigaDescrittiva && r.Quantita > 0)
                .OrderBy(r => r.NumeroRiga)
                .ToList();

            if (righe.Count == 0)
            {
                MessageBox.Show("Il preventivo non ha righe articolo convertibili.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show(
                $"Convertire il preventivo {preventivo.NumeroDocumento} in Ordine cliente?\n\nVerranno copiate le righe.",
                "Conferma conversione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            string GeneraNumeroOrdineCliente()
            {
                var last = _context.Ordini
                    .AsNoTracking()
                    .Where(o => o.TipoOrdine == "CLIENTE")
                    .OrderByDescending(o => o.Id)
                    .FirstOrDefault();

                var numero = 1;
                if (last != null)
                {
                    var digits = new string((last.NumeroOrdine ?? string.Empty).Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out var n)) numero = n + 1;
                }

                return $"OC{numero:D6}";
            }

            var imponibile = righe.Sum(r => r.Imponibile);
            var iva = righe.Sum(r => r.ImportoIVA);
            var totale = righe.Sum(r => r.Totale);

            var ordine = new Ordine
            {
                TipoOrdine = "CLIENTE",
                NumeroOrdine = GeneraNumeroOrdineCliente(),
                DataOrdine = DateTime.Today,
                ClienteId = preventivo.ClienteId,
                StatoOrdine = "APERTO",
                Imponibile = imponibile,
                IVA = iva,
                Totale = totale,
                Note = $"Generato da Preventivo {preventivo.NumeroDocumento}"
            };

            _context.Ordini.Add(ordine);
            await _context.SaveChangesAsync();

            var i = 1;
            foreach (var r in righe)
            {
                _context.OrdiniRighe.Add(new OrdineRiga
                {
                    OrdineId = ordine.Id,
                    NumeroRiga = i++,
                    ArticoloId = r.ArticoloId,
                    Descrizione = r.Descrizione,
                    QuantitaOrdinata = r.Quantita,
                    QuantitaEvasa = 0m,
                    UnitaMisura = r.UnitaMisura,
                    PrezzoUnitario = r.PrezzoUnitario,
                    Sconto = 0m,
                    AliquotaIVA = r.AliquotaIVA,
                    Totale = r.Totale
                });
            }

            await _context.SaveChangesAsync();

            MessageBox.Show("Ordine creato dal preventivo selezionato.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            var w = new OrdineEditWindow(_context, ordine.Id);
            w.ShowDialog();
        }
        catch (Exception ex)
        {
            UiLog.Error("PreventiviView.BtnConvertiInOrdine_Click", ex);
            MessageBox.Show($"Errore conversione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            await LoadPreventivi();
        }
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (PreventiviDataGrid.SelectedItem is not Documento doc)
        {
            MessageBox.Show("Seleziona un preventivo", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show("Eliminare il preventivo selezionato?", "Conferma", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            _context.Documenti.Remove(doc);
            await _context.SaveChangesAsync();
            await LoadPreventivi();
        }
        catch (Exception ex)
        {
            UiLog.Error("PreventiviView.BtnElimina_Click", ex);
            MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
