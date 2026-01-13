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

namespace DidoGest.UI.Views.Anagrafiche;

public partial class FornitoriView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Fornitore> _allFornitori = new();

    public FornitoriView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        
        Loaded += FornitoriView_Loaded;
    }

    private async void FornitoriView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadFornitori();
    }

    private async Task LoadFornitori()
    {
        try
        {
            _allFornitori = await _context.Fornitori.OrderBy(f => f.RagioneSociale).ToListAsync();
            FornitoriDataGrid.ItemsSource = _allFornitori;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("FornitoriView.LoadFornitori", ex);
            MessageBox.Show($"Errore caricamento fornitori: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleFornitori.Text = _allFornitori.Count.ToString();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            FornitoriDataGrid.ItemsSource = _allFornitori;
        }
        else
        {
            var filtered = _allFornitori.Where(f =>
                f.RagioneSociale.ToLower().Contains(searchText) ||
                (f.PartitaIVA != null && f.PartitaIVA.Contains(searchText)) ||
                f.Codice.ToLower().Contains(searchText)
            ).ToList();
            FornitoriDataGrid.ItemsSource = filtered;
        }
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Windows.FornitoreEditWindow(_context);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadFornitori();
        }
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (FornitoriDataGrid.SelectedItem is Fornitore fornitore)
        {
            var dialog = new Windows.FornitoreEditWindow(_context, fornitore.Id);
            if (dialog.ShowDialog() == true)
            {
                _ = LoadFornitori();
            }
        }
        else
        {
            MessageBox.Show("Seleziona un fornitore da modificare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void FornitoriDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (FornitoriDataGrid.SelectedItem is Fornitore fornitore)
        {
            var result = MessageBox.Show(
                $"Vuoi eliminare il fornitore '{fornitore.RagioneSociale}'?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _context.Fornitori.Remove(fornitore);
                    await _context.SaveChangesAsync();
                    await LoadFornitori();
                    MessageBox.Show("Fornitore eliminato", "Successo", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    UiLog.Error("FornitoriView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Seleziona un fornitore da eliminare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
