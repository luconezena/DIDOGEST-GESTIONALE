using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Services;
using DidoGest.UI.Windows;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Archivio;

public partial class ArchivioView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<dynamic> _allDocumenti = new();

    public ArchivioView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        
        Loaded += ArchivioView_Loaded;
    }

    private async void ArchivioView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDocumenti();
    }

    private async Task LoadDocumenti()
    {
        try
        {
            var documenti = await _context.DocumentiArchivio
                .OrderByDescending(d => d.DataProtocollo)
                .Select(d => new
                {
                    d.Id,
                    Protocollo = d.NumeroProtocollo,
                    DataDocumento = d.DataProtocollo,
                    Titolo = d.TitoloDocumento,
                    TipoDocumento = d.CategoriaDocumento ?? "",
                    Categoria = d.CategoriaDocumento ?? "",
                    NomeFile = System.IO.Path.GetFileName(d.PercorsoFile),
                    d.PercorsoFile
                })
                .ToListAsync();

            _allDocumenti = documenti.Cast<dynamic>().ToList();
            ArchivioDataGrid.ItemsSource = _allDocumenti;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("ArchivioView.LoadDocumenti", ex);
            MessageBox.Show($"Errore caricamento archivio: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleDocumenti.Text = _allDocumenti.Count.ToString();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            ArchivioDataGrid.ItemsSource = _allDocumenti;
        }
        else
        {
            var filtered = _allDocumenti.Where(d =>
                d.Titolo.ToLower().Contains(searchText) ||
                d.Protocollo.ToLower().Contains(searchText) ||
                (d.Categoria != null && d.Categoria.ToLower().Contains(searchText))
            ).ToList();
            ArchivioDataGrid.ItemsSource = filtered;
        }
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new DocumentoArchivioEditWindow(_context);
        if (w.ShowDialog() == true)
            _ = LoadDocumenti();
    }

    private void BtnApri_Click(object sender, RoutedEventArgs e)
    {
        if (ArchivioDataGrid.SelectedItem != null)
        {
            var doc = ArchivioDataGrid.SelectedItem as dynamic;
            string percorso = doc.PercorsoFile;
            
            if (!string.IsNullOrEmpty(percorso) && System.IO.File.Exists(percorso))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = percorso,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    UiLog.Error("ArchivioView.BtnApri_Click", ex);
                    MessageBox.Show($"Errore apertura file: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("File non trovato o percorso non valido", "Attenzione", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else
        {
            MessageBox.Show("Seleziona un documento da aprire", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ArchivioDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnApri_Click(sender, e);
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (ArchivioDataGrid.SelectedItem != null)
        {
            var doc = ArchivioDataGrid.SelectedItem as dynamic;
            var result = MessageBox.Show(
                $"Vuoi eliminare il documento '{doc.Titolo}'?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var documento = await _context.DocumentiArchivio.FindAsync((int)doc.Id);
                    if (documento != null)
                    {
                        _context.DocumentiArchivio.Remove(documento);
                        await _context.SaveChangesAsync();
                        await LoadDocumenti();
                        MessageBox.Show("Documento eliminato dall'archivio", "Successo", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    UiLog.Error("ArchivioView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        else
        {
            MessageBox.Show("Seleziona un documento da eliminare", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
