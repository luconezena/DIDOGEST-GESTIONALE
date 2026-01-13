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
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Magazzino;

public partial class ArticoliView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Articolo> _allArticoli = new();

    public ArticoliView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        
        Loaded += ArticoliView_Loaded;
    }

    private async void ArticoliView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadArticoli();
    }

    private async Task LoadArticoli()
    {
        try
        {
            _allArticoli = await _context.Articoli.OrderBy(a => a.Descrizione).ToListAsync();
            ArticoliDataGrid.ItemsSource = _allArticoli;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("ArticoliView.LoadArticoli", ex);
            MessageBox.Show($"Errore caricamento articoli: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleArticoli.Text = _allArticoli.Count.ToString();
        ArticoliAttivi.Text = _allArticoli.Count.ToString(); // Tutti attivi per default
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            ArticoliDataGrid.ItemsSource = _allArticoli;
        }
        else
        {
            var filtered = _allArticoli.Where(a =>
                a.Descrizione.ToLower().Contains(searchText) ||
                a.Codice.ToLower().Contains(searchText) ||
                (a.Categoria != null && a.Categoria.ToLower().Contains(searchText))
            ).ToList();
            ArticoliDataGrid.ItemsSource = filtered;
        }
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new ArticoloEditWindow(_context);
        if (w.ShowDialog() == true)
            _ = LoadArticoli();
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (ArticoliDataGrid.SelectedItem is Articolo articolo)
        {
            var w = new ArticoloEditWindow(_context, articolo.Id);
            if (w.ShowDialog() == true)
                _ = LoadArticoli();
        }
        else
        {
            MessageBox.Show("Seleziona un articolo da modificare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ArticoliDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (ArticoliDataGrid.SelectedItem is Articolo articolo)
        {
            var result = MessageBox.Show(
                $"Vuoi eliminare l'articolo '{articolo.Descrizione}'?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _context.Articoli.Remove(articolo);
                    await _context.SaveChangesAsync();
                    await LoadArticoli();
                    MessageBox.Show("Articolo eliminato", "Successo", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    UiLog.Error("ArticoliView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Seleziona un articolo da eliminare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
