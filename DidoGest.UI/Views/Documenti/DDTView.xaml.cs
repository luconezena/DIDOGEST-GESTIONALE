using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DidoGest.Data;
using DidoGest.UI.Services;
using DidoGest.UI.Windows;
using DidoGest.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.UI.Views.Documenti;

public partial class DDTView : UserControl
{
    private readonly DidoGestDbContext _context;
    private List<dynamic> _allDDT = new();

    public DDTView()
    {
        InitializeComponent();
        _context = DidoGestDb.CreateContext();
        
        Loaded += DDTView_Loaded;
    }

    private async void DDTView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDDT();
    }

    private async Task LoadDDT()
    {
        try
        {
            var ddtBase = await _context.Documenti
                .AsNoTracking()
                .Include(d => d.Cliente)
                .Include(d => d.Fornitore)
                .Where(d => d.TipoDocumento == "DDT")
                .OrderByDescending(d => d.DataDocumento)
                .Select(d => new
                {
                    d.Id,
                    d.NumeroDocumento,
                    d.DataDocumento,
                    ControparteRagioneSociale = d.Cliente != null
                        ? d.Cliente.RagioneSociale
                        : (d.Fornitore != null ? d.Fornitore.RagioneSociale : "N/D"),
                    TotaleDocumento = d.Totale
                })
                .ToListAsync();

            var ddtIds = ddtBase.Select(x => x.Id).ToList();

            // Fatture collegate direttamente (DocumentoOriginaleId)
            var fattureDirette = await _context.Documenti
                .AsNoTracking()
                .Where(f => f.DocumentoOriginaleId.HasValue
                            && ddtIds.Contains(f.DocumentoOriginaleId.Value)
                            && EF.Functions.Like(f.TipoDocumento, "%FATTURA%"))
                .OrderByDescending(f => f.DataDocumento)
                .Select(f => new { DdtId = f.DocumentoOriginaleId!.Value, f.Id, f.NumeroDocumento, f.DataDocumento })
                .ToListAsync();

            var mapDirette = fattureDirette
                .GroupBy(x => x.DdtId)
                .ToDictionary(g => g.Key, g => g.First());

            // Fatture collegate tramite tabella DocumentoCollegamenti
            var fattureLinked = await (from l in _context.DocumentoCollegamenti.AsNoTracking()
                                       join f in _context.Documenti.AsNoTracking() on l.DocumentoId equals f.Id
                                       where ddtIds.Contains(l.DocumentoOrigineId)
                                             && EF.Functions.Like(f.TipoDocumento, "%FATTURA%")
                                       orderby f.DataDocumento descending
                                       select new { DdtId = l.DocumentoOrigineId, f.Id, f.NumeroDocumento, f.DataDocumento })
                .ToListAsync();

            var mapLinked = fattureLinked
                .GroupBy(x => x.DdtId)
                .ToDictionary(g => g.Key, g => g.First());

            var ddt = ddtBase
                .Select(x =>
                {
                    var hasLinked = mapLinked.TryGetValue(x.Id, out var fl);
                    var hasDirect = mapDirette.TryGetValue(x.Id, out var fd);

                    var fattura = hasLinked ? fl : (hasDirect ? fd : null);
                    return new
                    {
                        x.Id,
                        x.NumeroDocumento,
                        x.DataDocumento,
                        x.ControparteRagioneSociale,
                        x.TotaleDocumento,
                        FatturaNumero = fattura?.NumeroDocumento,
                        FatturaData = fattura?.DataDocumento,
                        IsFatturato = fattura != null
                    };
                })
                .ToList();

            _allDDT = ddt.Cast<dynamic>().ToList();
            DDTDataGrid.ItemsSource = _allDDT;
            UpdateTotali();
        }
        catch (Exception ex)
        {
            UiLog.Error("DDTView.LoadDDT", ex);
            MessageBox.Show($"Errore caricamento DDT: {ex.Message}", "Errore", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotali()
    {
        TotaleDDT.Text = _allDDT.Count.ToString();
        DDTDaFatturare.Text = _allDDT.Count(d => !d.IsFatturato).ToString();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLower();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            DDTDataGrid.ItemsSource = _allDDT;
        }
        else
        {
            var filtered = _allDDT.Where(d =>
                d.NumeroDocumento.ToLower().Contains(searchText) ||
                d.ControparteRagioneSociale.ToLower().Contains(searchText)
            ).ToList();
            DDTDataGrid.ItemsSource = filtered;
        }
    }

    private void BtnNuovo_Click(object sender, RoutedEventArgs e)
    {
        var w = new DocumentoEditWindow(_context, "DDT", null);
        if (w.ShowDialog() == true)
            _ = LoadDDT();
    }

    private void DDTDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DDTDataGrid.SelectedItem == null) return;

        var ddt = DDTDataGrid.SelectedItem as dynamic;
        var id = (int)ddt.Id;

        var w = new DocumentoEditWindow(_context, "DDT", id);
        if (w.ShowDialog() == true)
            _ = LoadDDT();
    }

    private void BtnConverti_Click(object sender, RoutedEventArgs e)
    {
        if (DDTDataGrid.SelectedItem != null)
        {
            _ = ConvertiInFatturaAsync();
        }
        else
        {
            MessageBox.Show("Seleziona un DDT da convertire", "Attenzione", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ConvertiInFatturaAsync()
    {
        try
        {
            var ddt = DDTDataGrid.SelectedItem as dynamic;
            var ddtId = (int)ddt.Id;

            var doc = await _context.Documenti
                .AsNoTracking()
                .Include(d => d.Righe)
                .FirstOrDefaultAsync(d => d.Id == ddtId);
            if (doc == null)
            {
                MessageBox.Show("DDT non trovato.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!doc.ClienteId.HasValue)
            {
                MessageBox.Show("La conversione DDT→Fattura è disponibile solo per DDT clienti.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Anti-duplicato: se il DDT risulta già fatturato, non generiamo una nuova fattura.
            var fatturaEsistente = await _context.Documenti
                .AsNoTracking()
                .Where(f => f.DocumentoOriginaleId == doc.Id && EF.Functions.Like(f.TipoDocumento, "%FATTURA%"))
                .OrderByDescending(f => f.DataDocumento)
                .Select(f => new { f.Id, f.NumeroDocumento, f.DataDocumento, f.TipoDocumento })
                .FirstOrDefaultAsync();

            if (fatturaEsistente == null)
            {
                fatturaEsistente = await (from l in _context.DocumentoCollegamenti.AsNoTracking()
                                         join f in _context.Documenti.AsNoTracking() on l.DocumentoId equals f.Id
                                         where l.DocumentoOrigineId == doc.Id && EF.Functions.Like(f.TipoDocumento, "%FATTURA%")
                                         orderby f.DataDocumento descending
                                         select new { f.Id, f.NumeroDocumento, f.DataDocumento, f.TipoDocumento })
                    .FirstOrDefaultAsync();
            }

            if (fatturaEsistente != null)
            {
                var msg =
                    $"Questo DDT risulta già fatturato.\n\n" +
                    $"Documento: {fatturaEsistente.TipoDocumento} {fatturaEsistente.NumeroDocumento} del {fatturaEsistente.DataDocumento:dd/MM/yyyy}\n\n" +
                    "Vuoi aprire la fattura esistente?";

                var open = MessageBox.Show(
                    msg,
                    "DDT già fatturato",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (open == MessageBoxResult.Yes)
                {
                    var wExisting = new DocumentoEditWindow(_context, "FATTURA", fatturaEsistente.Id);
                    wExisting.ShowDialog();
                    await LoadDDT();
                }

                return;
            }

            var scelta = MessageBox.Show(
                "Scegli tipo di emissione:\n\n" +
                "SÌ = Fattura differita (consigliata: NON movimenta magazzino perché il DDT lo ha già fatto)\n" +
                "NO = Fattura immediata (ATTENZIONE: potrebbe movimentare il magazzino una seconda volta)\n" +
                "ANNULLA = Esci\n\n" +
                $"DDT: {doc.NumeroDocumento}",
                "DDT → Fattura",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (scelta == MessageBoxResult.Cancel)
                return;

            var creaDifferita = scelta == MessageBoxResult.Yes;

            if (!creaDifferita)
            {
                var warning = MessageBox.Show(
                    "Stai per creare una Fattura immediata partendo da un DDT.\n\n" +
                    "Questo può causare un DOPPIO movimento di magazzino (DDT + Fattura).\n\n" +
                    "Vuoi continuare comunque?",
                    "Attenzione",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warning != MessageBoxResult.Yes)
                    return;
            }

            var numeroFattura = await DocumentNumberService.GenerateNumeroDocumentoAsync(
                _context,
                "FATTURA",
                DateTime.Today);

            var giorniPagamento = await _context.Clienti
                .AsNoTracking()
                .Where(c => c.Id == doc.ClienteId)
                .Select(c => c.GiorniPagamento)
                .FirstOrDefaultAsync();

            var scadenza = DateTime.Today.AddDays((giorniPagamento ?? 30) < 0 ? 0 : (giorniPagamento ?? 30));

            var nuovaFattura = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = numeroFattura,
                DataDocumento = DateTime.Today,
                MagazzinoId = doc.MagazzinoId,
                ClienteId = doc.ClienteId,
                DataScadenzaPagamento = scadenza,
                Imponibile = doc.Imponibile,
                IVA = doc.IVA,
                Totale = doc.Totale,
                ScontoGlobale = doc.ScontoGlobale,
                SpeseAccessorie = doc.SpeseAccessorie,
                DocumentoOriginaleId = creaDifferita ? doc.Id : null,
                Note = creaDifferita
                    ? $"Generata da DDT {doc.NumeroDocumento}"
                    : $"Fattura immediata creata partendo da DDT {doc.NumeroDocumento}"
            };

            _context.Documenti.Add(nuovaFattura);
            await _context.SaveChangesAsync();

            var righe = (doc.Righe ?? new List<DocumentoRiga>()).OrderBy(r => r.NumeroRiga).ToList();
            foreach (var r in righe)
            {
                var nuovaRiga = new DocumentoRiga
                {
                    DocumentoId = nuovaFattura.Id,
                    NumeroRiga = r.NumeroRiga,
                    ArticoloId = r.ArticoloId,
                    Descrizione = r.Descrizione,
                    Quantita = r.Quantita,
                    UnitaMisura = r.UnitaMisura,
                    PrezzoUnitario = r.PrezzoUnitario,
                    Sconto1 = r.Sconto1,
                    Sconto2 = r.Sconto2,
                    Sconto3 = r.Sconto3,
                    PrezzoNetto = r.PrezzoNetto,
                    AliquotaIVA = r.AliquotaIVA,
                    Imponibile = r.Imponibile,
                    ImportoIVA = r.ImportoIVA,
                    Totale = r.Totale,
                    NumeroSerie = r.NumeroSerie,
                    Lotto = r.Lotto,
                    RigaDescrittiva = r.RigaDescrittiva,
                    Note = r.Note
                };
                _context.DocumentiRighe.Add(nuovaRiga);
            }

            if (righe.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            MessageBox.Show("Fattura creata dal DDT selezionato.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            var w = new DocumentoEditWindow(_context, "FATTURA", nuovaFattura.Id);
            w.ShowDialog();
            await LoadDDT();
        }
        catch (Exception ex)
        {
            UiLog.Error("DDTView.ConvertiInFatturaAsync", ex);
            MessageBox.Show($"Errore conversione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnFatturaDaSelezione_Click(object sender, RoutedEventArgs e)
    {
        if (DDTDataGrid.SelectedItems == null || DDTDataGrid.SelectedItems.Count < 2)
        {
            MessageBox.Show("Seleziona almeno 2 DDT per creare una fattura differita.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _ = CreaFatturaDifferitaDaSelezioneAsync();
    }

    private async Task CreaFatturaDifferitaDaSelezioneAsync()
    {
        try
        {
            var selectedIds = DDTDataGrid.SelectedItems
                .Cast<dynamic>()
                .Select(x => (int)x.Id)
                .Distinct()
                .ToList();

            var ddtDocs = await _context.Documenti
                .AsNoTracking()
                .Include(d => d.Righe)
                .Where(d => selectedIds.Contains(d.Id) && d.TipoDocumento == "DDT")
                .ToListAsync();

            if (ddtDocs.Count < 2)
            {
                MessageBox.Show("Selezione non valida.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var clienteId = ddtDocs.Select(d => d.ClienteId).Distinct().ToList();
            if (clienteId.Count != 1 || !clienteId[0].HasValue)
            {
                MessageBox.Show("I DDT selezionati devono essere tutti dello stesso cliente.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var magazzini = ddtDocs.Select(d => d.MagazzinoId).Distinct().ToList();
            if (magazzini.Count != 1)
            {
                MessageBox.Show("I DDT selezionati devono appartenere allo stesso magazzino.", "DIDO-GEST", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Verifica non già fatturati (diretti o tramite collegamenti)
            var ddtIds = ddtDocs.Select(d => d.Id).ToList();

            var giaFatturatiDiretti = await _context.Documenti
                .AsNoTracking()
                .Where(f => f.DocumentoOriginaleId.HasValue
                            && ddtIds.Contains(f.DocumentoOriginaleId.Value)
                            && EF.Functions.Like(f.TipoDocumento, "%FATTURA%"))
                .Select(f => f.DocumentoOriginaleId!.Value)
                .ToListAsync();

            var giaFatturatiLinked = await _context.DocumentoCollegamenti
                .AsNoTracking()
                .Where(l => ddtIds.Contains(l.DocumentoOrigineId))
                .Select(l => l.DocumentoOrigineId)
                .ToListAsync();

            var giaFatturati = giaFatturatiDiretti
                .Concat(giaFatturatiLinked)
                .Distinct()
                .ToHashSet();

            if (giaFatturati.Count > 0)
            {
                MessageBox.Show(
                    "Uno o più DDT selezionati risultano già fatturati.\n\n" +
                    "Togli dalla selezione i DDT già fatturati e riprova.",
                    "DIDO-GEST",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var lista = string.Join(", ", ddtDocs.OrderBy(d => d.DataDocumento).Select(d => d.NumeroDocumento));
            var confirm = MessageBox.Show(
                "Creare una Fattura differita dai DDT selezionati?\n\n" +
                $"DDT: {lista}",
                "Conferma",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            // Crea fattura (differita): per compatibilità teniamo DocumentoOriginaleId sul primo DDT.
            var numeroFattura = await DocumentNumberService.GenerateNumeroDocumentoAsync(
                _context,
                "FATTURA",
                DateTime.Today);

            var giorniPagamento = await _context.Clienti
                .AsNoTracking()
                .Where(c => c.Id == clienteId[0])
                .Select(c => c.GiorniPagamento)
                .FirstOrDefaultAsync();

            var scadenza = DateTime.Today.AddDays((giorniPagamento ?? 30) < 0 ? 0 : (giorniPagamento ?? 30));

            var first = ddtDocs.OrderBy(d => d.DataDocumento).ThenBy(d => d.Id).First();

            var imponibile = ddtDocs.Sum(d => d.Imponibile);
            var iva = ddtDocs.Sum(d => d.IVA);
            var totale = ddtDocs.Sum(d => d.Totale);

            var nuovaFattura = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = numeroFattura,
                DataDocumento = DateTime.Today,
                MagazzinoId = magazzini[0],
                ClienteId = clienteId[0],
                DataScadenzaPagamento = scadenza,
                Imponibile = imponibile,
                IVA = iva,
                Totale = totale,
                DocumentoOriginaleId = first.Id,
                Note = $"Fattura differita da DDT: {lista}"
            };

            _context.Documenti.Add(nuovaFattura);
            await _context.SaveChangesAsync();

            // Collega tutti i DDT selezionati alla fattura.
            foreach (var ddtDoc in ddtDocs)
            {
                _context.DocumentoCollegamenti.Add(new DocumentoCollegamento
                {
                    DocumentoId = nuovaFattura.Id,
                    DocumentoOrigineId = ddtDoc.Id
                });
            }

            // Copia righe (append) mantenendo l'ordine DDT → righe
            var numeroRiga = 1;
            foreach (var d in ddtDocs.OrderBy(x => x.DataDocumento).ThenBy(x => x.Id))
            {
                var righe = (d.Righe ?? new List<DocumentoRiga>())
                    .OrderBy(r => r.NumeroRiga)
                    .ToList();

                foreach (var r in righe)
                {
                    _context.DocumentiRighe.Add(new DocumentoRiga
                    {
                        DocumentoId = nuovaFattura.Id,
                        NumeroRiga = numeroRiga++,
                        ArticoloId = r.ArticoloId,
                        Descrizione = r.Descrizione,
                        Quantita = r.Quantita,
                        UnitaMisura = r.UnitaMisura,
                        PrezzoUnitario = r.PrezzoUnitario,
                        Sconto1 = r.Sconto1,
                        Sconto2 = r.Sconto2,
                        Sconto3 = r.Sconto3,
                        PrezzoNetto = r.PrezzoNetto,
                        AliquotaIVA = r.AliquotaIVA,
                        Imponibile = r.Imponibile,
                        ImportoIVA = r.ImportoIVA,
                        Totale = r.Totale,
                        NumeroSerie = r.NumeroSerie,
                        Lotto = r.Lotto,
                        RigaDescrittiva = r.RigaDescrittiva,
                        Note = r.Note
                    });
                }
            }

            await _context.SaveChangesAsync();

            MessageBox.Show("Fattura differita creata dai DDT selezionati.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
            var w = new DocumentoEditWindow(_context, "FATTURA", nuovaFattura.Id);
            w.ShowDialog();
            await LoadDDT();
        }
        catch (Exception ex)
        {
            UiLog.Error("DDTView.CreaFatturaDifferitaDaSelezioneAsync", ex);
            MessageBox.Show($"Errore creazione fattura: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnElimina_Click(object sender, RoutedEventArgs e)
    {
        if (DDTDataGrid.SelectedItem != null)
        {
            var ddt = DDTDataGrid.SelectedItem as dynamic;
            var result = MessageBox.Show(
                $"Vuoi eliminare il DDT {ddt.NumeroDocumento}?",
                "Conferma eliminazione",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var doc = await _context.Documenti.FindAsync((int)ddt.Id);
                    if (doc != null)
                    {
                        _context.Documenti.Remove(doc);
                        await _context.SaveChangesAsync();
                        await LoadDDT();
                        MessageBox.Show("DDT eliminato", "Successo", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    UiLog.Error("DDTView.BtnElimina_Click", ex);
                    MessageBox.Show($"Errore eliminazione: {ex.Message}", "Errore", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
