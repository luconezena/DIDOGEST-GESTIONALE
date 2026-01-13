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

public partial class AgentiView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Agente> _allAgenti = new();

    public AgentiView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        
        Loaded += AgentiView_Loaded;
    }

    private async void AgentiView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadAgenti();
    }

    private async Task LoadAgenti()
    {
        try
        {
            _allAgenti = await _context.Agenti.OrderBy(a => a.Cognome).ToListAsync();
            AgentiDataGrid.ItemsSource = _allAgenti;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("AgentiView.LoadAgenti", ex);
            MessageBox.Show($"Errore caricamento agenti: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleAgenti.Text = _allAgenti.Count.ToString();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            AgentiDataGrid.ItemsSource = _allAgenti;
        }
        else
        {
            var filtered = _allAgenti.Where(a =>
                a.Nome.ToLower().Contains(searchText) ||
                a.Cognome.ToLower().Contains(searchText) ||
                a.Codice.ToLower().Contains(searchText) ||
                (a.Email != null && a.Email.ToLower().Contains(searchText))
            ).ToList();
            AgentiDataGrid.ItemsSource = filtered;
        }
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Windows.AgenteEditWindow(_context);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadAgenti();
        }
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (AgentiDataGrid.SelectedItem is Agente agente)
        {
            var dialog = new Windows.AgenteEditWindow(_context, agente.Id);
            if (dialog.ShowDialog() == true)
            {
                _ = LoadAgenti();
            }
        }
        else
        {
            MessageBox.Show("Seleziona un agente da modificare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AgentiDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (AgentiDataGrid.SelectedItem is Agente agente)
        {
            var result = MessageBox.Show(
                $"Vuoi eliminare l'agente '{agente.Nome} {agente.Cognome}'?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _context.Agenti.Remove(agente);
                    await _context.SaveChangesAsync();
                    await LoadAgenti();
                    MessageBox.Show("Agente eliminato", "Successo", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    UiLog.Error("AgentiView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Seleziona un agente da eliminare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
