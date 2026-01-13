using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class RegistrazioneContabileEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _id;
    private RegistrazioneContabile? _entity;

    public RegistrazioneContabileEditWindow(DidoGestDbContext context, int? id = null)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        _context = context;
        _id = id;

        Loaded += RegistrazioneContabileEditWindow_Loaded;
    }

    private async void RegistrazioneContabileEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_id.HasValue)
        {
            _entity = await _context.RegistrazioniContabili.FindAsync(_id.Value);
            if (_entity == null)
            {
                MessageBox.Show("Registrazione non trovata.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
                Close();
                return;
            }

            Title = "Modifica Registrazione";
            DpData.SelectedDate = _entity.DataRegistrazione;
            TxtNumero.Text = _entity.NumeroRegistrazione;
            TxtCausale.Text = _entity.CausaleContabile ?? string.Empty;
            TxtDescrizione.Text = _entity.Descrizione ?? string.Empty;
            TxtDare.Text = _entity.TotaleDare.ToString(CultureInfo.CurrentCulture);
            TxtAvere.Text = _entity.TotaleAvere.ToString(CultureInfo.CurrentCulture);
        }
        else
        {
            Title = "Nuova Registrazione";
            DpData.SelectedDate = DateTime.Today;
            TxtNumero.Text = await GeneraNuovoNumero();
            TxtDare.Text = "0";
            TxtAvere.Text = "0";
        }
    }

    private async Task<string> GeneraNuovoNumero()
    {
        var year = DateTime.Today.Year;
        var prefix = $"PN{year}";

        var last = await _context.RegistrazioniContabili
            .AsNoTracking()
            .Where(r => r.NumeroRegistrazione.StartsWith(prefix))
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last != null)
        {
            var tail = last.NumeroRegistrazione.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) next = n + 1;
        }

        return $"{prefix}{next:D4}";
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (DpData.SelectedDate is null)
        {
            MessageBox.Show("Seleziona la data.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtNumero.Text))
        {
            MessageBox.Show("Numero registrazione obbligatorio.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(TxtDare.Text, out var dare) || !TryParseDecimal(TxtAvere.Text, out var avere))
        {
            MessageBox.Show("Totali Dare/Avere non validi.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_entity == null)
            {
                _entity = new RegistrazioneContabile();
                _context.RegistrazioniContabili.Add(_entity);
            }

            _entity.DataRegistrazione = DpData.SelectedDate.Value;
            _entity.NumeroRegistrazione = TxtNumero.Text.Trim();
            _entity.CausaleContabile = EmptyToNull(TxtCausale.Text);
            _entity.Descrizione = EmptyToNull(TxtDescrizione.Text);
            _entity.TotaleDare = dare;
            _entity.TotaleAvere = avere;

            await _context.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("RegistrazioneContabileEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? EmptyToNull(string? s)
    {
        s = (s ?? string.Empty).Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
        {
            value = 0m;
            return true;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
               || decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
