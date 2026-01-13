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

public partial class SottoscortaView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<SottoscortaRow> _allRows = new();

    public SottoscortaView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();

        Loaded += SottoscortaView_Loaded;
    }

    private async void SottoscortaView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadMagazzini();
        await LoadSottoscorta();
    }

    private async Task LoadMagazzini()
    {
        var mags = await _context.Magazzini.OrderBy(m => m.Codice).ToListAsync();
        var items = new List<ComboItem>
        {
            new ComboItem { Id = null, Display = "Tutti i magazzini" }
        };
        items.AddRange(mags.Select(m => new ComboItem { Id = m.Id, Display = $"{m.Codice} - {m.Descrizione}" }));
        CmbMagazzino.ItemsSource = items;
        CmbMagazzino.SelectedIndex = 0;
    }

    private async Task LoadSottoscorta()
    {
        try
        {
            var selected = (CmbMagazzino.SelectedItem as ComboItem)?.Id;

            var query = _context.GiacenzeMagazzino
                .AsNoTracking()
                .Include(g => g.Articolo)
                .Include(g => g.Magazzino)
                .AsQueryable();

            if (selected.HasValue)
            {
                query = query.Where(g => g.MagazzinoId == selected.Value);
            }

            var list = await query.ToListAsync();

            var grouped = list
                .Where(g => g.Articolo != null && g.Articolo.Attivo)
                .GroupBy(g => g.ArticoloId)
                .Select(grp => new
                {
                    ArticoloCodice = grp.First().Articolo!.Codice,
                    ArticoloDescrizione = grp.First().Articolo!.Descrizione,
                    ScortaMinima = grp.First().Articolo!.ScortaMinima,
                    QuantitaDisponibile = grp.Sum(x => x.QuantitaDisponibile),
                    MagazzinoCodice = selected.HasValue
                        ? grp.FirstOrDefault()?.Magazzino?.Codice ?? ""
                        : "Tutti"
                })
                .Where(x => x.ScortaMinima > 0 && x.QuantitaDisponibile < x.ScortaMinima)
                .OrderBy(x => x.ArticoloCodice)
                .ToList();

            _allRows = grouped.Select(x => new SottoscortaRow
            {
                ArticoloCodice = x.ArticoloCodice,
                ArticoloDescrizione = x.ArticoloDescrizione,
                MagazzinoCodice = x.MagazzinoCodice,
                QuantitaDisponibile = x.QuantitaDisponibile,
                ScortaMinima = x.ScortaMinima,
                QuantitaDaRiordinare = Math.Max(0, x.ScortaMinima - x.QuantitaDisponibile)
            }).ToList();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            UiLog.Error("SottoscortaView.LoadSottoscorta", ex);
            MessageBox.Show($"Errore caricamento sottoscorta: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        var s = SearchBox.Text?.ToLower() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(s)
            ? _allRows
            : _allRows.Where(r => r.ArticoloCodice.ToLower().Contains(s) || r.ArticoloDescrizione.ToLower().Contains(s)).ToList();

        SottoscortaDataGrid.ItemsSource = filtered;
        TotaleRighe.Text = filtered.Count.ToString();
    }

    private async void CmbMagazzino_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadSottoscorta();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilter();
    }

    private async void SottoscortaDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SottoscortaDataGrid.SelectedItem is not SottoscortaRow row) return;
        if (string.IsNullOrWhiteSpace(row.ArticoloCodice)) return;

        var articoloId = await _context.Articoli
            .AsNoTracking()
            .Where(a => a.Codice == row.ArticoloCodice)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (articoloId <= 0) return;

        var w = new ArticoloEditWindow(_context, articoloId);
        if (w.ShowDialog() == true)
            await LoadSottoscorta();
    }

    private sealed class ComboItem
    {
        public int? Id { get; set; }
        public string Display { get; set; } = string.Empty;
        public override string ToString() => Display;
    }

    private sealed class SottoscortaRow
    {
        public string ArticoloCodice { get; set; } = string.Empty;
        public string ArticoloDescrizione { get; set; } = string.Empty;
        public string MagazzinoCodice { get; set; } = string.Empty;
        public decimal QuantitaDisponibile { get; set; }
        public decimal ScortaMinima { get; set; }
        public decimal QuantitaDaRiordinare { get; set; }
    }
}
