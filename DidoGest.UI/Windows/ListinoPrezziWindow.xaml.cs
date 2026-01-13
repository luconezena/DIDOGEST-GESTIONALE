using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class ListinoPrezziWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int _listinoId;
    private List<Row> _rows = new();

    public ListinoPrezziWindow(DidoGestDbContext context, int listinoId)
    {
        InitializeComponent();
        _context = context;
        _listinoId = listinoId;

        Loaded += ListinoPrezziWindow_Loaded;
    }

    private async void ListinoPrezziWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var listino = await _context.Listini.FindAsync(_listinoId);
        LblTitolo.Text = listino != null ? $"Prezzi - {listino.Codice} ({listino.Descrizione})" : "Prezzi";

        var articoli = await _context.Articoli.OrderBy(a => a.Codice).ToListAsync();
        CmbArticolo.ItemsSource = articoli;
        CmbArticolo.DisplayMemberPath = "Codice";
        CmbArticolo.SelectedIndex = articoli.Count > 0 ? 0 : -1;

        TxtPrezzo.Text = "0";
        TxtSconto.Text = "0";

        await LoadRighe();
    }

    private async Task LoadRighe()
    {
        var list = await _context.ArticoliListino
            .AsNoTracking()
            .Include(al => al.Articolo)
            .Where(al => al.ListinoId == _listinoId)
            .OrderBy(al => al.Articolo!.Codice)
            .ToListAsync();

        _rows = list.Select(al => new Row
        {
            Id = al.Id,
            ArticoloCodice = al.Articolo?.Codice ?? "",
            ArticoloDescrizione = al.Articolo?.Descrizione ?? "",
            Prezzo = al.Prezzo,
            ScontoPercentuale = al.ScontoPercentuale
        }).ToList();

        PrezziDataGrid.ItemsSource = _rows;
        TotaleRighe.Text = _rows.Count.ToString();
    }

    private async void BtnAggiungi_Click(object sender, RoutedEventArgs e)
    {
        if (CmbArticolo.SelectedItem is not Articolo art)
        {
            MessageBox.Show("Seleziona un articolo", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtPrezzo.Text.Trim(), out var prezzo)) prezzo = 0m;
        if (!decimal.TryParse(TxtSconto.Text.Trim(), out var sconto)) sconto = 0m;

        try
        {
            var existing = await _context.ArticoliListino
                .FirstOrDefaultAsync(al => al.ListinoId == _listinoId && al.ArticoloId == art.Id);

            if (existing == null)
            {
                existing = new ArticoloListino
                {
                    ListinoId = _listinoId,
                    ArticoloId = art.Id,
                    DataInizioValidita = DateTime.Today,
                };
                _context.ArticoliListino.Add(existing);
            }

            existing.Prezzo = prezzo;
            existing.ScontoPercentuale = sconto;

            await _context.SaveChangesAsync();
            await LoadRighe();
        }
        catch (Exception ex)
        {
            UiLog.Error("ListinoPrezziWindow.BtnAggiungi_Click", ex);
            MessageBox.Show($"Errore salvataggio prezzo: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (PrezziDataGrid.SelectedItem is not Row row)
        {
            MessageBox.Show("Seleziona una riga", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show("Eliminare il prezzo selezionato?", "Conferma", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            var entity = await _context.ArticoliListino.FindAsync(row.Id);
            if (entity != null)
            {
                _context.ArticoliListino.Remove(entity);
                await _context.SaveChangesAsync();
            }
            await LoadRighe();
        }
        catch (Exception ex)
        {
            UiLog.Error("ListinoPrezziWindow.BtnElimina_Click", ex);
            MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string ArticoloCodice { get; set; } = string.Empty;
        public string ArticoloDescrizione { get; set; } = string.Empty;
        public decimal Prezzo { get; set; }
        public decimal ScontoPercentuale { get; set; }
    }
}
