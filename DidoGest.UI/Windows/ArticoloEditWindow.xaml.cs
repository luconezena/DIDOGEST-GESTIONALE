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

public partial class ArticoloEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _articoloId;
    private Articolo? _articolo;

    public ArticoloEditWindow(DidoGestDbContext context, int? articoloId = null)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        _context = context;
        _articoloId = articoloId;

        Loaded += ArticoloEditWindow_Loaded;
    }

    private async void ArticoloEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadFornitori();

        if (_articoloId.HasValue)
        {
            _articolo = await _context.Articoli.FindAsync(_articoloId.Value);
            if (_articolo != null)
            {
                Title = "Modifica Articolo";
                LoadArticolo();
            }
        }
        else
        {
            Title = "Nuovo Articolo";
            TxtCodice.Text = await GeneraNuovoCodice();
            ChkAttivo.IsChecked = true;
            TxtAliquota.Text = "22";
            TxtPrezzoAcquisto.Text = "0";
            TxtPrezzoVendita.Text = "0";
            TxtScortaMinima.Text = "0";
        }
    }

    private async Task LoadFornitori()
    {
        var fornitori = await _context.Fornitori.AsNoTracking().OrderBy(f => f.RagioneSociale).ToListAsync();
        CmbFornitore.ItemsSource = fornitori;
        CmbFornitore.DisplayMemberPath = "RagioneSociale";
        CmbFornitore.SelectedValuePath = "Id";
    }

    private void LoadArticolo()
    {
        if (_articolo == null) return;

        TxtCodice.Text = _articolo.Codice;
        TxtEAN.Text = _articolo.CodiceEAN ?? string.Empty;
        TxtDescrizione.Text = _articolo.Descrizione;
        TxtDescrizioneEstesa.Text = _articolo.DescrizioneEstesa ?? string.Empty;
        TxtCategoria.Text = _articolo.Categoria ?? string.Empty;
        TxtSottocategoria.Text = _articolo.Sottocategoria ?? string.Empty;
        TxtMarca.Text = _articolo.Marca ?? string.Empty;
        TxtUM.Text = _articolo.UnitaMisura ?? string.Empty;
        TxtPrezzoAcquisto.Text = _articolo.PrezzoAcquisto.ToString(CultureInfo.CurrentCulture);
        TxtPrezzoVendita.Text = _articolo.PrezzoVendita.ToString(CultureInfo.CurrentCulture);
        TxtAliquota.Text = _articolo.AliquotaIVA.ToString(CultureInfo.CurrentCulture);
        TxtScortaMinima.Text = _articolo.ScortaMinima.ToString(CultureInfo.CurrentCulture);
        TxtNote.Text = _articolo.Note ?? string.Empty;

        ChkAttivo.IsChecked = _articolo.Attivo;
        ChkLotti.IsChecked = _articolo.GestioneLotti;
        ChkSeriali.IsChecked = _articolo.GestioneNumeriSerie;
        ChkServizio.IsChecked = _articolo.ArticoloDiServizio;

        if (_articolo.FornitorePredefinitoId.HasValue)
            CmbFornitore.SelectedValue = _articolo.FornitorePredefinitoId.Value;
    }

    private async Task<string> GeneraNuovoCodice()
    {
        var ultimo = await _context.Articoli.OrderByDescending(a => a.Id).FirstOrDefaultAsync();
        var next = 1;
        if (ultimo != null)
        {
            var s = (ultimo.Codice ?? string.Empty).Trim();
            if (s.StartsWith("ART", StringComparison.OrdinalIgnoreCase))
            {
                var num = new string(s.SkipWhile(c => !char.IsDigit(c)).ToArray());
                if (int.TryParse(num, out var n)) next = n + 1;
            }
        }

        return $"ART{next:D5}";
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCodice.Text) || string.IsNullOrWhiteSpace(TxtDescrizione.Text))
        {
            MessageBox.Show("Codice e Descrizione sono obbligatori", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(TxtPrezzoAcquisto.Text, out var prezzoAcq) ||
            !TryParseDecimal(TxtPrezzoVendita.Text, out var prezzoVen) ||
            !TryParseDecimal(TxtAliquota.Text, out var aliquota) ||
            !TryParseDecimal(TxtScortaMinima.Text, out var scorta))
        {
            MessageBox.Show("Verifica i valori numerici (prezzi/IVA/scorta)", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_articolo == null)
            {
                _articolo = new Articolo();
                _context.Articoli.Add(_articolo);
            }

            _articolo.Codice = TxtCodice.Text.Trim();
            _articolo.Descrizione = TxtDescrizione.Text.Trim();
            _articolo.CodiceEAN = EmptyToNull(TxtEAN.Text);
            _articolo.DescrizioneEstesa = EmptyToNull(TxtDescrizioneEstesa.Text);
            _articolo.Categoria = EmptyToNull(TxtCategoria.Text);
            _articolo.Sottocategoria = EmptyToNull(TxtSottocategoria.Text);
            _articolo.Marca = EmptyToNull(TxtMarca.Text);
            _articolo.UnitaMisura = EmptyToNull(TxtUM.Text);
            _articolo.PrezzoAcquisto = prezzoAcq;
            _articolo.PrezzoVendita = prezzoVen;
            _articolo.AliquotaIVA = aliquota;
            _articolo.ScortaMinima = scorta;
            _articolo.Note = EmptyToNull(TxtNote.Text);

            _articolo.Attivo = ChkAttivo.IsChecked == true;
            _articolo.GestioneLotti = ChkLotti.IsChecked == true;
            _articolo.GestioneNumeriSerie = ChkSeriali.IsChecked == true;
            _articolo.ArticoloDiServizio = ChkServizio.IsChecked == true;
            _articolo.DataModifica = DateTime.Now;

            _articolo.FornitorePredefinitoId = CmbFornitore.SelectedValue is int id ? id : null;

            await _context.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("ArticoloEditWindow.BtnSalva_Click", ex);
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
