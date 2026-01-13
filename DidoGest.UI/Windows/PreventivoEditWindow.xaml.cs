using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class PreventivoEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _documentoId;
    private Documento? _doc;

    public PreventivoEditWindow(DidoGestDbContext context, int? documentoId = null)
    {
        InitializeComponent();
        _context = context;
        _documentoId = documentoId;

        Loaded += PreventivoEditWindow_Loaded;
    }

    private async void PreventivoEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var clienti = await _context.Clienti.OrderBy(c => c.RagioneSociale).ToListAsync();
        CmbCliente.ItemsSource = clienti;
        CmbCliente.DisplayMemberPath = "RagioneSociale";

        if (_documentoId.HasValue)
        {
            _doc = await _context.Documenti.Include(d => d.Cliente).FirstOrDefaultAsync(d => d.Id == _documentoId.Value);
            if (_doc != null)
            {
                Title = "Modifica Preventivo";
                TxtNumero.Text = _doc.NumeroDocumento;
                DpData.SelectedDate = _doc.DataDocumento;
                if (_doc.ClienteId.HasValue)
                {
                    CmbCliente.SelectedItem = clienti.FirstOrDefault(c => c.Id == _doc.ClienteId.Value);
                }
                TxtNote.Text = _doc.Note;
                return;
            }
        }

        Title = "Nuovo Preventivo";
        TxtNumero.Text = await GeneraNumero();
        DpData.SelectedDate = DateTime.Today;
        CmbCliente.SelectedIndex = clienti.Count > 0 ? 0 : -1;
    }

    private async Task<string> GeneraNumero()
    {
        return await DocumentNumberService.GenerateNumeroDocumentoAsync(
            _context,
            "PREVENTIVO",
            DateTime.Today);
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        if (CmbCliente.SelectedItem is not Cliente cliente)
        {
            MessageBox.Show("Seleziona un cliente", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var numero = (TxtNumero.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(numero))
            {
                MessageBox.Show("Numero preventivo obbligatorio", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existingId = await _context.Documenti
                .AsNoTracking()
                .Where(d => d.TipoDocumento == "PREVENTIVO" && d.NumeroDocumento == numero)
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            if (existingId != 0 && (_doc == null || existingId != _doc.Id))
            {
                MessageBox.Show(
                    "Esiste già un preventivo con lo stesso numero.",
                    "Attenzione",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (_doc == null)
            {
                var magazzinoIdDefault = await _context.Magazzini
                    .AsNoTracking()
                    .OrderByDescending(m => m.Principale)
                    .ThenBy(m => m.Id)
                    .Select(m => m.Id)
                    .FirstOrDefaultAsync();
                if (magazzinoIdDefault == 0) magazzinoIdDefault = 1;

                _doc = new Documento
                {
                    TipoDocumento = "PREVENTIVO",
                    MagazzinoId = magazzinoIdDefault,
                    Imponibile = 0m,
                    IVA = 0m,
                    Totale = 0m
                };
                _context.Documenti.Add(_doc);
            }

            _doc.NumeroDocumento = numero;
            _doc.DataDocumento = DpData.SelectedDate ?? DateTime.Today;
            _doc.ClienteId = cliente.Id;
            _doc.Note = TxtNote.Text?.Trim();

            await _context.SaveChangesAsync();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("PreventivoEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
