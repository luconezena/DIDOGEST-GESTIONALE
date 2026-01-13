using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace DidoGest.UI.Windows;

public partial class DocumentoArchivioEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _id;
    private DocumentoArchivio? _entity;

    public DocumentoArchivioEditWindow(DidoGestDbContext context, int? id = null)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
        _context = context;
        _id = id;

        Loaded += DocumentoArchivioEditWindow_Loaded;
    }

    private async void DocumentoArchivioEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_id.HasValue)
        {
            _entity = await _context.DocumentiArchivio.FindAsync(_id.Value);
            if (_entity == null)
            {
                MessageBox.Show("Documento non trovato.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
                Close();
                return;
            }

            Title = "Modifica documento archiviato";
            TxtProtocollo.Text = _entity.NumeroProtocollo;
            DpData.SelectedDate = _entity.DataProtocollo;
            TxtTitolo.Text = _entity.TitoloDocumento;
            TxtCategoria.Text = _entity.CategoriaDocumento ?? string.Empty;
            TxtTags.Text = _entity.Tags ?? string.Empty;
            TxtFile.Text = _entity.PercorsoFile;
            TxtDescrizione.Text = _entity.Descrizione ?? string.Empty;
            TxtNote.Text = _entity.Note ?? string.Empty;
        }
        else
        {
            Title = "Nuovo documento archiviato";
            DpData.SelectedDate = DateTime.Today;
            TxtProtocollo.Text = await GeneraNuovoProtocollo();
        }
    }

    private async Task<string> GeneraNuovoProtocollo()
    {
        var year = DateTime.Today.Year;
        var prefix = $"PRO{year}";

        var last = await _context.DocumentiArchivio
            .AsNoTracking()
            .Where(d => d.NumeroProtocollo.StartsWith(prefix))
            .OrderByDescending(d => d.Id)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last != null)
        {
            var tail = last.NumeroProtocollo.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) next = n + 1;
        }

        return $"{prefix}{next:D4}";
    }

    private void BtnSfoglia_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Seleziona un file",
            Filter = "Tutti i file|*.*"
        };

        if (dlg.ShowDialog() == true)
            TxtFile.Text = dlg.FileName;
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

        if (string.IsNullOrWhiteSpace(TxtProtocollo.Text) || string.IsNullOrWhiteSpace(TxtTitolo.Text))
        {
            MessageBox.Show("Protocollo e Titolo sono obbligatori.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtFile.Text) || !File.Exists(TxtFile.Text))
        {
            MessageBox.Show("Seleziona un file valido.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_entity == null)
            {
                _entity = new DocumentoArchivio();
                _context.DocumentiArchivio.Add(_entity);
            }

            _entity.NumeroProtocollo = TxtProtocollo.Text.Trim();
            _entity.DataProtocollo = DpData.SelectedDate.Value;
            _entity.TitoloDocumento = TxtTitolo.Text.Trim();
            _entity.CategoriaDocumento = EmptyToNull(TxtCategoria.Text);
            _entity.Tags = EmptyToNull(TxtTags.Text);
            _entity.PercorsoFile = TxtFile.Text.Trim();
            _entity.EstensioneFile = Path.GetExtension(_entity.PercorsoFile);
            _entity.DimensioneFile = new FileInfo(_entity.PercorsoFile).Length;
            _entity.Descrizione = EmptyToNull(TxtDescrizione.Text);
            _entity.Note = EmptyToNull(TxtNote.Text);
            _entity.DataModifica = DateTime.Now;

            await _context.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("DocumentoArchivioEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string? EmptyToNull(string? s)
    {
        s = (s ?? string.Empty).Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }
}
