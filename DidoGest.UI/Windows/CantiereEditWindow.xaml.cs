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

public partial class CantiereEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _id;
    private Cantiere? _entity;

    public CantiereEditWindow(int? id)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;

        _context = DidoGestDb.CreateContext();

        _id = id;
        Loaded += async (_, _) => await InitAsync();
    }

    private async Task InitAsync()
    {
        var clienti = await _context.Clienti.AsNoTracking().OrderBy(c => c.RagioneSociale).ToListAsync();
        CmbCliente.ItemsSource = clienti;
        CmbCliente.DisplayMemberPath = "RagioneSociale";

        if (_id.HasValue)
        {
            _entity = await _context.Cantieri.FirstOrDefaultAsync(x => x.Id == _id.Value);
            if (_entity == null)
            {
                MessageBox.Show("Cantiere non trovato.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
                Close();
                return;
            }

            Title = "Modifica Cantiere";
            TxtCodice.Text = _entity.CodiceCantiere;
            TxtDescrizione.Text = _entity.Descrizione;
            TxtIndirizzo.Text = _entity.Indirizzo ?? string.Empty;
            TxtCitta.Text = _entity.Citta ?? string.Empty;
            DpInizio.SelectedDate = _entity.DataInizio;
            DpFine.SelectedDate = _entity.DataFine;
            TxtImporto.Text = _entity.ImportoPreventivato.ToString(CultureInfo.CurrentCulture);
            TxtResponsabile.Text = _entity.ResponsabileCantiere ?? string.Empty;
            TxtCosti.Text = _entity.CostiSostenuti.ToString(CultureInfo.CurrentCulture);
            TxtRicavi.Text = _entity.RicaviMaturati.ToString(CultureInfo.CurrentCulture);
            TxtStato.Text = _entity.StatoCantiere ?? string.Empty;
            TxtNote.Text = _entity.Note ?? string.Empty;

            CmbCliente.SelectedItem = clienti.FirstOrDefault(c => c.Id == _entity.ClienteId);
        }
        else
        {
            Title = "Nuovo Cantiere";
            _entity = null;

            TxtCodice.Text = $"CAN-{DateTime.Now:yyyyMMddHHmm}";
            DpInizio.SelectedDate = DateTime.Today;
            TxtStato.Text = "APERTO";
            TxtImporto.Text = "0";
            TxtCosti.Text = "0";
            TxtRicavi.Text = "0";
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtCodice.Text))
        {
            MessageBox.Show("Il codice cantiere è obbligatorio.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtDescrizione.Text))
        {
            MessageBox.Show("La descrizione è obbligatoria.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CmbCliente.SelectedItem is not Cliente cliente)
        {
            MessageBox.Show("Seleziona un cliente.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DpInizio.SelectedDate is null)
        {
            MessageBox.Show("Seleziona la data di inizio.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(TxtImporto.Text, out var importo))
        {
            MessageBox.Show("Importo preventivato non valido.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(TxtCosti.Text, out var costi))
        {
            MessageBox.Show("Costi sostenuti non validi.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(TxtRicavi.Text, out var ricavi))
        {
            MessageBox.Show("Ricavi maturati non validi.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Cantiere entity;
            if (_id.HasValue)
            {
                entity = await _context.Cantieri.FirstAsync(x => x.Id == _id.Value);
            }
            else
            {
                entity = new Cantiere();
                _context.Cantieri.Add(entity);
            }

            entity.CodiceCantiere = TxtCodice.Text.Trim();
            entity.Descrizione = TxtDescrizione.Text.Trim();
            entity.ClienteId = cliente.Id;
            entity.Indirizzo = string.IsNullOrWhiteSpace(TxtIndirizzo.Text) ? null : TxtIndirizzo.Text.Trim();
            entity.Citta = string.IsNullOrWhiteSpace(TxtCitta.Text) ? null : TxtCitta.Text.Trim();
            entity.DataInizio = DpInizio.SelectedDate.Value;
            entity.DataFine = DpFine.SelectedDate;
            entity.ImportoPreventivato = importo;
            entity.ResponsabileCantiere = string.IsNullOrWhiteSpace(TxtResponsabile.Text) ? null : TxtResponsabile.Text.Trim();
            entity.CostiSostenuti = costi;
            entity.RicaviMaturati = ricavi;
            entity.StatoCantiere = string.IsNullOrWhiteSpace(TxtStato.Text) ? null : TxtStato.Text.Trim();
            entity.Note = string.IsNullOrWhiteSpace(TxtNote.Text) ? null : TxtNote.Text.Trim();

            await _context.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("CantiereEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
