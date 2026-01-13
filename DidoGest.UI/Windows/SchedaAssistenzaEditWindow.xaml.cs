using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DidoGest.Core.Entities;
using DidoGest.Data;
using DidoGest.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Windows;

public partial class SchedaAssistenzaEditWindow : Window
{
    private readonly DidoGestDbContext _context;
    private readonly int? _id;

    public SchedaAssistenzaEditWindow(int? id)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;

        _context = DidoGestDb.CreateContext();

        _id = id;

        Loaded += async (_, _) => await Init();
    }

    private async Task Init()
    {
        var clienti = await _context.Clienti.AsNoTracking().OrderBy(c => c.RagioneSociale).ToListAsync();
        CmbCliente.ItemsSource = clienti;
        CmbCliente.DisplayMemberPath = "RagioneSociale";

        CmbStato.ItemsSource = new[] { "APERTA", "IN_LAVORAZIONE", "SOSPESA", "COMPLETATA", "CONSEGNATA" };
        CmbStato.SelectedIndex = 0;

        if (_id.HasValue)
        {
            var entity = await _context.SchedeAssistenza.FirstOrDefaultAsync(x => x.Id == _id.Value);
            if (entity == null) return;

            TxtNumero.Text = entity.NumeroScheda ?? string.Empty;
            DpDataApertura.SelectedDate = entity.DataApertura == default ? DateTime.Today : entity.DataApertura;

            CmbCliente.SelectedItem = clienti.FirstOrDefault(c => c.Id == entity.ClienteId);

            if (!string.IsNullOrWhiteSpace(entity.StatoLavorazione))
                CmbStato.SelectedItem = entity.StatoLavorazione;

            TxtProdotto.Text = entity.DescrizioneProdotto ?? string.Empty;
            TxtMatricola.Text = entity.Matricola ?? string.Empty;
            TxtModello.Text = entity.Modello ?? string.Empty;
            TxtTecnico.Text = entity.TecnicoAssegnato ?? string.Empty;
            TxtDifetto.Text = entity.DifettoDichiarato ?? string.Empty;
            TxtNote.Text = entity.Note ?? string.Empty;
        }
        else
        {
            TxtNumero.Text = $"ASS-{DateTime.Now:yyyyMMddHHmm}";
            DpDataApertura.SelectedDate = DateTime.Today;
        }
    }

    private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSalva_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SchedaAssistenza entity;
            if (_id.HasValue)
            {
                entity = await _context.SchedeAssistenza.FirstAsync(x => x.Id == _id.Value);
            }
            else
            {
                entity = new SchedaAssistenza();
                _context.SchedeAssistenza.Add(entity);
            }

            if (CmbCliente.SelectedItem is not Cliente cliente)
            {
                MessageBox.Show("Seleziona un cliente.", "Dati mancanti", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            entity.NumeroScheda = (TxtNumero.Text ?? string.Empty).Trim();
            entity.DataApertura = DpDataApertura.SelectedDate ?? DateTime.Today;
            entity.ClienteId = cliente.Id;
            entity.StatoLavorazione = (CmbStato.SelectedItem as string) ?? "APERTA";

            entity.DescrizioneProdotto = (TxtProdotto.Text ?? string.Empty).Trim();
            entity.Matricola = (TxtMatricola.Text ?? string.Empty).Trim();
            entity.Modello = (TxtModello.Text ?? string.Empty).Trim();
            entity.TecnicoAssegnato = (TxtTecnico.Text ?? string.Empty).Trim();
            entity.DifettoDichiarato = (TxtDifetto.Text ?? string.Empty).Trim();
            entity.Note = (TxtNote.Text ?? string.Empty).Trim();

            await _context.SaveChangesAsync();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            UiLog.Error("SchedaAssistenzaEditWindow.BtnSalva_Click", ex);
            MessageBox.Show($"Errore salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
