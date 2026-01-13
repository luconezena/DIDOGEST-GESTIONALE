using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Contabilita;

public partial class RegistriIVAView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<dynamic> _allRegistri = new();

    public RegistriIVAView()
    {
        InitializeComponent();
            _context = DidoGestDb.CreateContext();
        
        DataDa.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        DataA.SelectedDate = DateTime.Now;
        
        Loaded += RegistriIVAView_Loaded;
    }

    private async void RegistriIVAView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadRegistri();
    }

    private async Task LoadRegistri()
    {
        try
        {
            var dataDa = DataDa.SelectedDate ?? DateTime.Now.AddMonths(-1);
            var dataA = DataA.SelectedDate ?? DateTime.Now;
            var tipoReg = (TipoRegistro.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Vendite";

            var registri = await _context.RegistriIVA
                .Where(r => r.DataRegistrazione >= dataDa && 
                            r.DataRegistrazione <= dataA &&
                            r.TipoRegistro == tipoReg)
                .OrderBy(r => r.DataRegistrazione)
                .ThenBy(r => r.NumeroProtocollo)
                .Select(r => new
                {
                    r.Id,
                    r.DataRegistrazione,
                    r.NumeroProtocollo,
                    NumeroDocumento = "",
                    Anagrafica = r.Descrizione ?? "",
                    r.Imponibile,
                    r.ImportoIVA,
                    Totale = r.Imponibile + r.ImportoIVA
                })
                .ToListAsync();

            _allRegistri = registri.Cast<dynamic>().ToList();
            RegistriDataGrid.ItemsSource = _allRegistri;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("RegistriIVAView.LoadRegistri", ex);
            MessageBox.Show($"Errore caricamento registri: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleMovimenti.Text = _allRegistri.Count.ToString();
        var imponibile = _allRegistri.Sum(r => (decimal)r.Imponibile);
        var iva = _allRegistri.Sum(r => (decimal)r.ImportoIVA);
        var totale = _allRegistri.Sum(r => (decimal)r.Totale);
        
        TotaleImponibile.Text = imponibile.ToString("C2");
        TotaleIVA.Text = iva.ToString("C2");
        TotaleDocumento.Text = totale.ToString("C2");
    }

    private async void TipoRegistro_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await LoadRegistri();
        }
    }

    private async void BtnCerca_Click(object sender, RoutedEventArgs e)
    {
        await LoadRegistri();
    }

    private void BtnStampa_Click(object sender, RoutedEventArgs e)
    {
        var tipoReg = (TipoRegistro.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Vendite";
        MessageBox.Show($"Stampa Registro IVA {tipoReg}\n\nPeriodo: {DataDa.SelectedDate:dd/MM/yyyy} - {DataA.SelectedDate:dd/MM/yyyy}", 
            "Stampa Registro", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
