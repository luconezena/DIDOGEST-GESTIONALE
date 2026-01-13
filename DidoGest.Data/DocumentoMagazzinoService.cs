using System;
using System.Linq;
using System.Threading.Tasks;
using DidoGest.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data.Services;

public sealed class DocumentoMagazzinoService
{
    private readonly DidoGestDbContext _context;

    public DocumentoMagazzinoService(DidoGestDbContext context)
    {
        _context = context;
    }

    public async Task SyncMovimentiMagazzinoForDocumentoAsync(
        int documentoId,
        string tipoDocumento,
        string numeroDocumento,
        DateTime dataDocumento,
        int? documentoOriginaleId)
    {
        var tipoUpper = (tipoDocumento ?? string.Empty).ToUpperInvariant();

        var doc = await _context.Documenti
            .AsNoTracking()
            .Where(d => d.Id == documentoId)
            .Select(d => new { d.ClienteId, d.FornitoreId, d.MagazzinoId })
            .FirstOrDefaultAsync();
        if (doc == null) return;

        var magazzinoId = doc.MagazzinoId;
        if (magazzinoId <= 0) magazzinoId = 1;

        var isVendita = doc.ClienteId.HasValue && !doc.FornitoreId.HasValue;
        var isAcquisto = doc.FornitoreId.HasValue && !doc.ClienteId.HasValue;
        if (!isVendita && !isAcquisto)
        {
            // Documento senza controparte univoca: non movimentare (evita errori/ambiguità).
            return;
        }

        // Regola MVP:
        // - DDT movimenta sempre
        // - FATTURA_ACCOMPAGNATORIA movimenta sempre (documento immediato che include la movimentazione)
        // - Fatture: movimentano solo se NON sono differite (DocumentoOriginaleId == null)
        var movimenta =
            tipoUpper == "DDT" ||
            tipoUpper == "FATTURA_ACCOMPAGNATORIA" ||
            (tipoUpper.Contains("FATTURA") && documentoOriginaleId == null);
        if (!movimenta) return;

        var righe = await _context.DocumentiRighe
            .AsNoTracking()
            .Where(r => r.DocumentoId == documentoId)
            .OrderBy(r => r.NumeroRiga)
            .ToListAsync();

        var articoloIds = righe
            .Where(r => r.ArticoloId.HasValue && !r.RigaDescrittiva && r.Quantita > 0)
            .Select(r => r.ArticoloId!.Value)
            .Distinct()
            .ToList();

        // Rimuovi movimenti precedenti del documento (idempotenza)
        var old = await _context.MovimentiMagazzino
            .Where(m => m.DocumentoId == documentoId)
            .ToListAsync();
        if (old.Count > 0)
            _context.MovimentiMagazzino.RemoveRange(old);

        if (articoloIds.Count == 0)
        {
            // Nessuna riga articolo: basta pulire eventuali movimenti residui.
            await _context.SaveChangesAsync();
            return;
        }

        var articoli = await _context.Articoli
            .AsNoTracking()
            .Where(a => articoloIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id);

        foreach (var r in righe)
        {
            if (!r.ArticoloId.HasValue) continue;
            if (r.RigaDescrittiva) continue;
            if (r.Quantita <= 0) continue;

            if (!articoli.TryGetValue(r.ArticoloId.Value, out var art)) continue;
            if (art.ArticoloDiServizio) continue;

            var tipoMov = isAcquisto ? "CARICO" : "SCARICO";

            _context.MovimentiMagazzino.Add(new MovimentoMagazzino
            {
                ArticoloId = art.Id,
                MagazzinoId = magazzinoId,
                TipoMovimento = tipoMov,
                Quantita = r.Quantita,
                CostoUnitario = art.PrezzoAcquisto,
                DataMovimento = dataDocumento,
                NumeroDocumento = numeroDocumento,
                DocumentoId = documentoId,
                DocumentoRigaId = r.Id,
                Causale = tipoUpper
            });
        }

        await _context.SaveChangesAsync();

        // Ricalcola giacenze per gli articoli toccati (solo magazzino del documento)
        // Nota: il provider SQLite non supporta SUM su decimal via SQL; calcoliamo lato client.
        var movAgg = await _context.MovimentiMagazzino
            .AsNoTracking()
            .Where(m => m.MagazzinoId == magazzinoId && articoloIds.Contains(m.ArticoloId))
            .Select(m => new { m.ArticoloId, m.TipoMovimento, m.Quantita })
            .ToListAsync();

        var mapQty = movAgg
            .GroupBy(m => m.ArticoloId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.TipoMovimento == "CARICO" ? x.Quantita : x.TipoMovimento == "SCARICO" ? -x.Quantita : 0m));

        var giacenze = await _context.GiacenzeMagazzino
            .Where(g => g.MagazzinoId == magazzinoId && articoloIds.Contains(g.ArticoloId))
            .ToListAsync();

        foreach (var artId in articoloIds)
        {
            var qty = mapQty.TryGetValue(artId, out var v) ? v : 0m;
            var g = giacenze.FirstOrDefault(x => x.ArticoloId == artId);
            if (g == null)
            {
                _context.GiacenzeMagazzino.Add(new GiacenzaMagazzino
                {
                    ArticoloId = artId,
                    MagazzinoId = magazzinoId,
                    Quantita = qty,
                    QuantitaImpegnata = 0m,
                    DataUltimoAggiornamento = DateTime.Now
                });
            }
            else
            {
                g.Quantita = qty;
                g.DataUltimoAggiornamento = DateTime.Now;
            }
        }

        await _context.SaveChangesAsync();

        // Aggiorna giacenza totale articolo (somma giacenze)
        var giacTot = await _context.GiacenzeMagazzino
            .AsNoTracking()
            .Where(g => articoloIds.Contains(g.ArticoloId))
            .Select(g => new { g.ArticoloId, g.Quantita })
            .ToListAsync();
        var totMap = giacTot
            .GroupBy(x => x.ArticoloId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantita));

        var artToUpdate = await _context.Articoli.Where(a => articoloIds.Contains(a.Id)).ToListAsync();
        foreach (var a in artToUpdate)
        {
            if (totMap.TryGetValue(a.Id, out var t))
                a.GiacenzaTotale = t;
        }

        await _context.SaveChangesAsync();
    }
}
