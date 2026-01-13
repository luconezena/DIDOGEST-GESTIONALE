using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Services;
using DidoGest.UI.Windows;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Magazzino;

public partial class MovimentiView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<MovimentoRow> _allRows = new();

    public MovimentiView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();

        Loaded += MovimentiView_Loaded;
    }

    private async void MovimentiView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadMovimenti();
    }

    private async Task LoadMovimenti()
    {
        try
        {
            var list = await _context.MovimentiMagazzino
                .AsNoTracking()
                .Include(m => m.Articolo)
                .Include(m => m.Magazzino)
                .OrderByDescending(m => m.DataMovimento)
                .Take(500)
                .ToListAsync();

            _allRows = list.Select(m => new MovimentoRow
            {
                Id = m.Id,
                DataMovimento = m.DataMovimento,
                TipoMovimento = m.TipoMovimento,
                ArticoloCodice = m.Articolo?.Codice ?? "",
                ArticoloDescrizione = m.Articolo?.Descrizione ?? "",
                MagazzinoCodice = m.Magazzino?.Codice ?? "",
                Quantita = m.Quantita,
                CostoUnitario = m.CostoUnitario,
                NumeroDocumento = m.NumeroDocumento,
                Causale = m.Causale
            }).ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            UiLog.Error("MovimentiView.LoadMovimenti", ex);
            MessageBox.Show($"Errore caricamento movimenti: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        var s = (SearchBox.Text ?? string.Empty).ToLower();
        var filtered = string.IsNullOrWhiteSpace(s)
            ? _allRows
            : _allRows.Where(r =>
                r.TipoMovimento.ToLower().Contains(s) ||
                r.ArticoloCodice.ToLower().Contains(s) ||
                r.ArticoloDescrizione.ToLower().Contains(s) ||
                (r.MagazzinoCodice != null && r.MagazzinoCodice.ToLower().Contains(s)) ||
                (r.NumeroDocumento != null && r.NumeroDocumento.ToLower().Contains(s)) ||
                (r.Causale != null && r.Causale.ToLower().Contains(s))
            ).ToList();

        MovimentiDataGrid.ItemsSource = filtered;
        TotaleMovimenti.Text = filtered.Count.ToString();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Windows.MovimentoMagazzinoEditWindow(_context);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadMovimenti();
        }
    }

    private async void MovimentiDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (MovimentiDataGrid.SelectedItem is not MovimentoRow row) return;
        if (string.IsNullOrWhiteSpace(row.ArticoloCodice)) return;

        var articoloId = await _context.Articoli
            .AsNoTracking()
            .Where(a => a.Codice == row.ArticoloCodice)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (articoloId <= 0) return;

        var w = new ArticoloEditWindow(_context, articoloId);
        w.ShowDialog();
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (MovimentiDataGrid.SelectedItem is not MovimentoRow row)
        {
            MessageBox.Show("Seleziona un movimento da eliminare", "Attenzione",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show("Eliminare il movimento selezionato?\n\nNota: la giacenza non verrà ricalcolata automaticamente.",
            "Conferma eliminazione", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            var entity = await _context.MovimentiMagazzino.FindAsync(row.Id);
            if (entity != null)
            {
                _context.MovimentiMagazzino.Remove(entity);
                await _context.SaveChangesAsync();
            }
            await LoadMovimenti();
        }
        catch (Exception ex)
        {
            UiLog.Error("MovimentiView.BtnElimina_Click", ex);
            MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed class MovimentoRow
    {
        public int Id { get; set; }
        public DateTime DataMovimento { get; set; }
        public string TipoMovimento { get; set; } = string.Empty;
        public string ArticoloCodice { get; set; } = string.Empty;
        public string ArticoloDescrizione { get; set; } = string.Empty;
        public string? MagazzinoCodice { get; set; }
        public decimal Quantita { get; set; }
        public decimal CostoUnitario { get; set; }
        public string? NumeroDocumento { get; set; }
        public string? Causale { get; set; }
    }
}
