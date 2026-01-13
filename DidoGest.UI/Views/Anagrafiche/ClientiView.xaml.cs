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

public partial class ClientiView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<Cliente> _allClienti = new();

    public ClientiView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        
        Loaded += ClientiView_Loaded;
    }

    private async void ClientiView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadClienti();
    }

    private async Task LoadClienti()
    {
        try
        {
            _allClienti = await _context.Clienti.OrderBy(c => c.RagioneSociale).ToListAsync();
            ClientiDataGrid.ItemsSource = _allClienti;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("ClientiView.LoadClienti", ex);
            MessageBox.Show($"Errore caricamento clienti: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleClienti.Text = _allClienti.Count.ToString();
        ClientiAttivi.Text = _allClienti.Count.ToString(); // Tutti attivi per default
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            ClientiDataGrid.ItemsSource = _allClienti;
        }
        else
        {
            var filtered = _allClienti.Where(c =>
                c.RagioneSociale.ToLower().Contains(searchText) ||
                (c.PartitaIVA != null && c.PartitaIVA.Contains(searchText)) ||
                (c.CodiceFiscale != null && c.CodiceFiscale.Contains(searchText)) ||
                c.Codice.ToLower().Contains(searchText)
            ).ToList();
            ClientiDataGrid.ItemsSource = filtered;
        }
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Windows.ClienteEditWindow(_context);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadClienti();
        }
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (ClientiDataGrid.SelectedItem is Cliente cliente)
        {
            var dialog = new Windows.ClienteEditWindow(_context, cliente.Id);
            if (dialog.ShowDialog() == true)
            {
                _ = LoadClienti();
            }
        }
        else
        {
            MessageBox.Show("Seleziona un cliente da modificare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ClientiDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (ClientiDataGrid.SelectedItem is Cliente cliente)
        {
            var result = MessageBox.Show(
                $"Vuoi eliminare il cliente '{cliente.RagioneSociale}'?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _context.Clienti.Remove(cliente);
                    await _context.SaveChangesAsync();
                    await LoadClienti();
                    MessageBox.Show("Cliente eliminato", "Successo", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    UiLog.Error("ClientiView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Seleziona un cliente da eliminare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
