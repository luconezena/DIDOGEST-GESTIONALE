using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class MovimentoMagazzinoEditWindow : Window
{
    private readonly DidoGestDbContext _context;

    public MovimentoMagazzinoEditWindow(DidoGestDbContext context)
    {
        InitializeComponent();
        _context = context;

        Loaded += MovimentoMagazzinoEditWindow_Loaded;
    }

    private async void MovimentoMagazzinoEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CmbTipo.ItemsSource = new[] { "CARICO", "SCARICO" };
        CmbTipo.SelectedIndex = 0;

        var magazzini = await _context.Magazzini.OrderBy(m => m.Codice).ToListAsync();
        CmbMagazzino.ItemsSource = magazzini;
        CmbMagazzino.DisplayMemberPath = "Codice";
        CmbMagazzino.SelectedIndex = magazzini.Count > 0 ? 0 : -1;

        var articoli = await _context.Articoli.OrderBy(a => a.Codice).ToListAsync();
        CmbArticolo.ItemsSource = articoli;
        CmbArticolo.DisplayMemberPath = "Codice";
        CmbArticolo.SelectedIndex = articoli.Count > 0 ? 0 : -1;

        DpData.SelectedDate = DateTime.Today;
        TxtQuantita.Text = "1";
        TxtCosto.Text = "0";
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (CmbMagazzino.SelectedItem is not Magazzino mag) {
            MessageBox.Show("Seleziona un magazzino", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (CmbArticolo.SelectedItem is not Articolo art) {
            MessageBox.Show("Seleziona un articolo", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtQuantita.Text.Trim(), out var qty) || qty <= 0)
        {
            MessageBox.Show("Quantità non valida", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtCosto.Text.Trim(), out var costo))
        {
            costo = 0m;
        }

        var tipo = (CmbTipo.SelectedItem as string) ?? "CARICO";
        var data = DpData.SelectedDate ?? DateTime.Today;

        try
        {
            // Aggiorna giacenza
            var giacenza = await _context.GiacenzeMagazzino
                .FirstOrDefaultAsync(g => g.MagazzinoId == mag.Id && g.ArticoloId == art.Id);

            if (giacenza == null)
            {
                giacenza = new GiacenzaMagazzino
                {
                    MagazzinoId = mag.Id,
                    ArticoloId = art.Id,
                    Quantita = 0m,
                    QuantitaImpegnata = 0m,
                    DataUltimoAggiornamento = DateTime.Now
                };
                _context.GiacenzeMagazzino.Add(giacenza);
            }

            if (tipo == "SCARICO" && giacenza.QuantitaDisponibile < qty)
            {
                MessageBox.Show($"Disponibilità insufficiente. Disponibile: {giacenza.QuantitaDisponibile:N2}",
                    "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (tipo == "CARICO")
            {
                giacenza.Quantita += qty;
            }
            else
            {
                giacenza.Quantita -= qty;
            }

            giacenza.DataUltimoAggiornamento = DateTime.Now;

            var mov = new MovimentoMagazzino
            {
                ArticoloId = art.Id,
                MagazzinoId = mag.Id,
                TipoMovimento = tipo,
                Quantita = qty,
                CostoUnitario = costo,
                DataMovimento = data,
                NumeroDocumento = TxtNumeroDocumento.Text.Trim(),
                Causale = TxtCausale.Text.Trim(),
                Note = TxtNote.Text.Trim(),
                UtenteCreazione = Environment.UserName
            };
            _context.MovimentiMagazzino.Add(mov);

            await _context.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("MovimentoMagazzinoEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
