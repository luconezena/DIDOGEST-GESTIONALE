using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MagazzinoEntity = DidoGest.Core.Entities.Magazzino;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Magazzino;

public partial class MagazziniView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<MagazzinoEntity> _allMagazzini = new();

    public MagazziniView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();

        Loaded += MagazziniView_Loaded;
    }

    private async void MagazziniView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadMagazzini();
    }

    private async Task LoadMagazzini()
    {
        try
        {
            _allMagazzini = await _context.Magazzini.OrderBy(m => m.Codice).ToListAsync();
            MagazziniDataGrid.ItemsSource = _allMagazzini;
            TotaleMagazzini.Text = _allMagazzini.Count.ToString();
        }
        catch (Exception ex)
        {
            UiLog.Error("MagazziniView.LoadMagazzini", ex);
            MessageBox.Show($"Errore caricamento magazzini: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var s = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(s))
        {
            MagazziniDataGrid.ItemsSource = _allMagazzini;
            return;
        }

        MagazziniDataGrid.ItemsSource = _allMagazzini.Where(m =>
            m.Codice.ToLower().Contains(s) ||
            m.Descrizione.ToLower().Contains(s) ||
            (m.Citta != null && m.Citta.ToLower().Contains(s))
        ).ToList();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Windows.MagazzinoEditWindow(_context);
        if (dialog.ShowDialog() == true)
        {
            _ = LoadMagazzini();
        }
    }

    private void BtnModifica_Click(object sender, RoutedEventArgs e)
    {
        if (MagazziniDataGrid.SelectedItem is MagazzinoEntity mag)
        {
            var dialog = new Windows.MagazzinoEditWindow(_context, mag.Id);
            if (dialog.ShowDialog() == true)
            {
                _ = LoadMagazzini();
            }
        }
        else
        {
            MessageBox.Show("Seleziona un magazzino da modificare", "Attenzione",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void MagazziniDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnModifica_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (MagazziniDataGrid.SelectedItem is not MagazzinoEntity mag)
        {
            MessageBox.Show("Seleziona un magazzino da eliminare", "Attenzione",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var res = MessageBox.Show($"Vuoi eliminare il magazzino '{mag.Codice} - {mag.Descrizione}'?",
            "Conferma eliminazione", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            _context.Magazzini.Remove(mag);
            await _context.SaveChangesAsync();
            await LoadMagazzini();
        }
        catch (Exception ex)
        {
            UiLog.Error("MagazziniView.BtnElimina_Click", ex);
            MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
