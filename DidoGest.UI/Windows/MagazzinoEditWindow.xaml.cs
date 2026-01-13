using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class MagazzinoEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _magazzinoId;
    private Magazzino? _magazzino;

    public MagazzinoEditWindow(DidoGestDbContext context, int? magazzinoId = null)
    {
        InitializeComponent();
        _context = context;
        _magazzinoId = magazzinoId;

        Loaded += MagazzinoEditWindow_Loaded;
    }

    private async void MagazzinoEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_magazzinoId.HasValue)
        {
            _magazzino = await _context.Magazzini.FindAsync(_magazzinoId.Value);
            if (_magazzino != null)
            {
                LoadMagazzino();
                Title = "Modifica Magazzino";
            }
        }
        else
        {
            Title = "Nuovo Magazzino";
            TxtCodice.Text = await GeneraNuovoCodice();
        }
    }

    private void LoadMagazzino()
    {
        if (_magazzino == null) return;

        TxtCodice.Text = _magazzino.Codice;
        TxtDescrizione.Text = _magazzino.Descrizione;
        TxtIndirizzo.Text = _magazzino.Indirizzo;
        TxtCAP.Text = _magazzino.CAP;
        TxtCitta.Text = _magazzino.Citta;
        TxtTelefono.Text = _magazzino.Telefono;
        ChkPrincipale.IsChecked = _magazzino.Principale;
        ChkAttivo.IsChecked = _magazzino.Attivo;
        TxtNote.Text = _magazzino.Note;
    }

    private async Task<string> GeneraNuovoCodice()
    {
        var ultimo = await _context.Magazzini.OrderByDescending(m => m.Id).FirstOrDefaultAsync();

        int numero = 1;
        if (ultimo != null)
        {
            var codice = ultimo.Codice.Replace("MAG", "");
            if (int.TryParse(codice, out int n))
            {
                numero = n + 1;
            }
        }

        return $"MAG{numero:D2}";
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCodice.Text) || string.IsNullOrWhiteSpace(TxtDescrizione.Text))
        {
            MessageBox.Show("Codice e Descrizione sono obbligatori", "Attenzione",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_magazzino == null)
            {
                _magazzino = new Magazzino();
                _context.Magazzini.Add(_magazzino);
            }

            _magazzino.Codice = TxtCodice.Text.Trim();
            _magazzino.Descrizione = TxtDescrizione.Text.Trim();
            _magazzino.Indirizzo = TxtIndirizzo.Text.Trim();
            _magazzino.CAP = TxtCAP.Text.Trim();
            _magazzino.Citta = TxtCitta.Text.Trim();
            _magazzino.Telefono = TxtTelefono.Text.Trim();
            _magazzino.Principale = ChkPrincipale.IsChecked == true;
            _magazzino.Attivo = ChkAttivo.IsChecked == true;
            _magazzino.Note = TxtNote.Text.Trim();

            if (_magazzino.Principale)
            {
                var altri = _context.Magazzini.Where(m => m.Id != _magazzino.Id && m.Principale);
                foreach (var a in altri)
                {
                    a.Principale = false;
                }
            }

            await _context.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("MagazzinoEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
