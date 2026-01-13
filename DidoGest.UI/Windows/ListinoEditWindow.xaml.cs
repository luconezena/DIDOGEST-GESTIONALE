using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class ListinoEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _listinoId;
    private Listino? _listino;

    public ListinoEditWindow(DidoGestDbContext context, int? listinoId = null)
    {
        InitializeComponent();
        _context = context;
        _listinoId = listinoId;

        Loaded += ListinoEditWindow_Loaded;
    }

    private async void ListinoEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_listinoId.HasValue)
        {
            _listino = await _context.Listini.FindAsync(_listinoId.Value);
            if (_listino != null)
            {
                LoadListino();
                Title = "Modifica Listino";
            }
        }
        else
        {
            Title = "Nuovo Listino";
            TxtCodice.Text = await GeneraNuovoCodice();
            DpInizio.SelectedDate = DateTime.Today;
        }
    }

    private void LoadListino()
    {
        if (_listino == null) return;

        TxtCodice.Text = _listino.Codice;
        TxtDescrizione.Text = _listino.Descrizione;
        DpInizio.SelectedDate = _listino.DataInizioValidita;
        DpFine.SelectedDate = _listino.DataFineValidita;
        ChkAttivo.IsChecked = _listino.Attivo;
        TxtNote.Text = _listino.Note;
    }

    private async Task<string> GeneraNuovoCodice()
    {
        var ultimo = await _context.Listini.OrderByDescending(l => l.Id).FirstOrDefaultAsync();
        int numero = 1;
        if (ultimo != null)
        {
            var digits = new string(ultimo.Codice.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int n))
            {
                numero = n + 1;
            }
        }
        return $"LST{numero:D4}";
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCodice.Text) || string.IsNullOrWhiteSpace(TxtDescrizione.Text))
        {
            MessageBox.Show("Codice e Descrizione sono obbligatori", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var inizio = DpInizio.SelectedDate ?? DateTime.Today;
        var fine = DpFine.SelectedDate;

        try
        {
            if (_listino == null)
            {
                _listino = new Listino();
                _context.Listini.Add(_listino);
            }

            _listino.Codice = TxtCodice.Text.Trim();
            _listino.Descrizione = TxtDescrizione.Text.Trim();
            _listino.DataInizioValidita = inizio;
            _listino.DataFineValidita = fine;
            _listino.Attivo = ChkAttivo.IsChecked == true;
            _listino.Note = TxtNote.Text.Trim();

            await _context.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("ListinoEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
