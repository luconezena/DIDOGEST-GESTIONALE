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

namespace DidoGest.UI.Views.Magazzino;

public partial class ListiniView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Listino> _allListini = new();

    public ListiniView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();

        Loaded += ListiniView_Loaded;
    }

    private async void ListiniView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadListini();
    }

    private async Task LoadListini()
    {
        try
        {
            _allListini = await _context.Listini.OrderBy(l => l.Codice).ToListAsync();
            ListiniDataGrid.ItemsSource = _allListini;
            TotaleListini.Text = _allListini.Count.ToString();
        }
        catch (Exception ex)
        {
            UiLog.Error("ListiniView.LoadListini", ex);
            MessageBox.Show($"Errore caricamento listini: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var s = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(s))
        {
            ListiniDataGrid.ItemsSource = _allListini;
            return;
        }

        ListiniDataGrid.ItemsSource = _allListini.Where(l =>
            l.Codice.ToLower().Contains(s) ||
            l.Descrizione.ToLower().Contains(s)
        ).ToList();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Windows.ListinoEditWindow(_context);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadListini();
        }
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (ListiniDataGrid.SelectedItem is Listino listino)
        {
            var dialog = new Windows.ListinoEditWindow(_context, listino.Id);
            if (dialog.ShowDialog() == true)
            {
                _ = LoadListini();
            }
        }
        else
        {
            MessageBox.Show("Seleziona un listino", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ListiniDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private void BtnPrezzi_Click(object sender, RoutedEventArgs e)
    {
        if (ListiniDataGrid.SelectedItem is not Listino listino)
        {
            MessageBox.Show("Seleziona un listino", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new Windows.ListinoPrezziWindow(_context, listino.Id);
        dialog.ShowDialog();
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (ListiniDataGrid.SelectedItem is not Listino listino)
        {
            MessageBox.Show("Seleziona un listino da eliminare", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show($"Vuoi eliminare il listino '{listino.Codice}'?",
            "Conferma eliminazione", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            _context.Listini.Remove(listino);
            await _context.SaveChangesAsync();
            await LoadListini();
        }
        catch (Exception ex)
        {
            UiLog.Error("ListiniView.BtnElimina_Click", ex);
            MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
