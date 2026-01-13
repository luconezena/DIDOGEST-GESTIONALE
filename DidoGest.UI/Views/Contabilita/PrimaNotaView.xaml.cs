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

namespace DidoGest.UI.Views.Contabilita;

public partial class PrimaNotaView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<dynamic> _allRegistrazioni = new();

    public PrimaNotaView()
    {
        InitializeComponent();
            _context = DidoGestDb.CreateContext();
        
        DataDa.SelectedDate = DateTime.Now.AddMonths(-1);
        DataA.SelectedDate = DateTime.Now;
        
        Loaded += PrimaNotaView_Loaded;
    }

    private async void PrimaNotaView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRegistrazioni();
    }

    private async Task LoadRegistrazioni()
    {
        try
        {
            var dataDa = DataDa.SelectedDate ?? DateTime.Now.AddMonths(-1);
            var dataA = DataA.SelectedDate ?? DateTime.Now;

            var registrazioni = await _context.RegistrazioniContabili
                .Where(r => r.DataRegistrazione >= dataDa && r.DataRegistrazione <= dataA)
                .OrderByDescending(r => r.DataRegistrazione)
                .Select(r => new
                {
                    r.Id,
                    r.DataRegistrazione,
                    r.NumeroRegistrazione,
                    Causale = r.CausaleContabile ?? "",
                    r.Descrizione,
                    r.TotaleDare,
                    r.TotaleAvere
                })
                .ToListAsync();

            _allRegistrazioni = registrazioni.Cast<dynamic>().ToList();
            PrimaNotaDataGrid.ItemsSource = _allRegistrazioni;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("PrimaNotaView.LoadRegistrazioni", ex);
            MessageBox.Show($"Errore caricamento registrazioni: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleRegistrazioni.Text = _allRegistrazioni.Count.ToString();
        var totaleDare = _allRegistrazioni.Sum(r => (decimal)r.TotaleDare);
        var totaleAvere = _allRegistrazioni.Sum(r => (decimal)r.TotaleAvere);
        var saldo = totaleDare - totaleAvere;
        Saldo.Text = saldo.ToString("C2");
    }

    private async void BtnCerca_Click(object sender, RoutedEventArgs e)
    {
        await LoadRegistrazioni();
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new RegistrazioneContabileEditWindow(_context);
        if (w.ShowDialog() == true)
            _ = LoadRegistrazioni();
    }

    private void PrimaNotaDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (PrimaNotaDataGrid.SelectedItem == null) return;

        var reg = PrimaNotaDataGrid.SelectedItem as dynamic;
        var id = (int)reg.Id;

        var w = new RegistrazioneContabileEditWindow(_context, id);
        if (w.ShowDialog() == true)
            _ = LoadRegistrazioni();
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (PrimaNotaDataGrid.SelectedItem != null)
        {
            var reg = PrimaNotaDataGrid.SelectedItem as dynamic;
            var result = MessageBox.Show(
                $"Vuoi eliminare la registrazione n. {reg.NumeroRegistrazione}?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var registrazione = await _context.RegistrazioniContabili.FindAsync((int)reg.Id);
                    if (registrazione != null)
                    {
                        _context.RegistrazioniContabili.Remove(registrazione);
                        await _context.SaveChangesAsync();
                        await LoadRegistrazioni();
                        MessageBox.Show("Registrazione eliminata", "Successo", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    UiLog.Error("PrimaNotaView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Seleziona una registrazione da eliminare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
