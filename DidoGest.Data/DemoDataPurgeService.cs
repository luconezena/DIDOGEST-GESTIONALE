using Microsoft.EntityFrameworkCore;
using DidoGest.Core.Entities;

namespace DidoGest.Data;

public sealed record DemoDataPurgeResult(
    int Clienti,
    int Fornitori,
    int Articoli,
    int Ordini,
    int RigheOrdine,
    int Documenti,
    int RigheDocumento,
    int MovimentiMagazzino,
    int GiacenzeMagazzino,
    int Contratti,
    int SchedeAssistenza,
    int AssistenzaInterventi)
{
    public int Totale => Clienti + Fornitori + Articoli + Ordini + RigheOrdine + Documenti + RigheDocumento + MovimentiMagazzino + GiacenzeMagazzino + Contratti + SchedeAssistenza + AssistenzaInterventi;
}

public static class DemoDataPurgeService
{
    private const string DemoPrefix = "DIDOGEST-DEMO";

    public static DemoDataPurgeResult PurgeDemoData(DidoGestDbContext dbContext)
    {
        if (dbContext is null) throw new ArgumentNullException(nameof(dbContext));

        using var tx = dbContext.Database.BeginTransaction();

        var demoClienteIds = dbContext.Clienti
            .Where(c => c.Codice.StartsWith(DemoPrefix))
            .Select(c => c.Id)
            .ToList();

        var demoFornitoreIds = dbContext.Fornitori
            .Where(f => f.Codice.StartsWith(DemoPrefix))
            .Select(f => f.Id)
            .ToList();

        var demoArticoloIds = dbContext.Articoli
            .Where(a => a.Codice.StartsWith(DemoPrefix))
            .Select(a => a.Id)
            .ToList();

        // Contratti DEMO (FK su Cliente: vanno rimossi prima dei clienti)
        var demoContrattoIds = dbContext.Contratti
            .Where(c =>
                c.NumeroContratto.StartsWith(DemoPrefix) ||
                (c.Note != null && c.Note.Contains(DemoPrefix)) ||
                demoClienteIds.Contains(c.ClienteId))
            .Select(c => c.Id)
            .ToList();

        // Schede assistenza DEMO (FK su Cliente: vanno rimossi prima dei clienti)
        var demoSchedaIds = dbContext.SchedeAssistenza
            .Where(s =>
                s.NumeroScheda.StartsWith(DemoPrefix) ||
                (s.Note != null && s.Note.Contains(DemoPrefix)) ||
                demoClienteIds.Contains(s.ClienteId))
            .Select(s => s.Id)
            .ToList();

        // Interventi assistenza DEMO (FK su Scheda)
        var demoAssistenzaInterventi = dbContext.AssistenzeInterventi
            .Where(i =>
                demoSchedaIds.Contains(i.SchedaAssistenzaId) ||
                (i.Note != null && i.Note.Contains(DemoPrefix)))
            .ToList();

        // Ordini DEMO (FK restrict su Cliente/Fornitore: vanno rimossi prima)
        var demoOrdineIds = dbContext.Ordini
            .Where(o =>
                (o.Note != null && o.Note.Contains(DemoPrefix)) ||
                (o.ClienteId != null && demoClienteIds.Contains(o.ClienteId.Value)) ||
                (o.FornitoreId != null && demoFornitoreIds.Contains(o.FornitoreId.Value)))
            .Select(o => o.Id)
            .ToList();

        // Documenti DEMO: note generate in seed + qualsiasi documento agganciato a demo cliente/fornitore.
        var demoDocumentoIds = dbContext.Documenti
            .Where(d =>
                (d.Note != null && (d.Note.Contains("DEMO generato automaticamente") || d.Note.Contains("DIDOGEST-DEMO"))) ||
                (d.ClienteId != null && demoClienteIds.Contains(d.ClienteId.Value)) ||
                (d.FornitoreId != null && demoFornitoreIds.Contains(d.FornitoreId.Value)) ||
                (d.RagioneSocialeDestinatario != null && d.RagioneSocialeDestinatario.Contains("DIDOGEST DEMO")))
            .Select(d => d.Id)
            .ToList();

        var righe = dbContext.DocumentiRighe
            .Where(r =>
                demoDocumentoIds.Contains(r.DocumentoId) ||
                (r.ArticoloId != null && demoArticoloIds.Contains(r.ArticoloId.Value)) ||
                (r.Note != null && r.Note.Contains("DEMO")))
            .ToList();

        var movimenti = dbContext.MovimentiMagazzino
            .Where(m =>
                demoArticoloIds.Contains(m.ArticoloId) ||
                (m.DocumentoId != null && demoDocumentoIds.Contains(m.DocumentoId.Value)) ||
                (m.NumeroDocumento != null && m.NumeroDocumento.StartsWith(DemoPrefix)) ||
                (m.Note != null && m.Note.Contains("DEMO")) ||
                (m.Causale != null && m.Causale.Contains("DEMO")))
            .ToList();

        var giacenze = dbContext.GiacenzeMagazzino
            .Where(g => demoArticoloIds.Contains(g.ArticoloId))
            .ToList();

        var righeOrdine = dbContext.OrdiniRighe
            .Where(r => demoOrdineIds.Contains(r.OrdineId) || (r.Note != null && r.Note.Contains("DEMO")))
            .ToList();

        // Elimina Assistenza (FK-safe: interventi -> schede)
        if (demoAssistenzaInterventi.Count > 0)
        {
            dbContext.AssistenzeInterventi.RemoveRange(demoAssistenzaInterventi);
            dbContext.SaveChanges();
        }

        if (demoSchedaIds.Count > 0)
        {
            var schede = dbContext.SchedeAssistenza.Where(s => demoSchedaIds.Contains(s.Id)).ToList();
            if (schede.Count > 0)
            {
                dbContext.SchedeAssistenza.RemoveRange(schede);
                dbContext.SaveChanges();
            }
        }

        // Contratti
        if (demoContrattoIds.Count > 0)
        {
            var contratti = dbContext.Contratti.Where(c => demoContrattoIds.Contains(c.Id)).ToList();
            if (contratti.Count > 0)
            {
                dbContext.Contratti.RemoveRange(contratti);
                dbContext.SaveChanges();
            }
        }

        // Elimina in ordine FK-safe
        if (movimenti.Count > 0)
        {
            dbContext.MovimentiMagazzino.RemoveRange(movimenti);
            dbContext.SaveChanges();
        }

        if (righeOrdine.Count > 0)
        {
            dbContext.OrdiniRighe.RemoveRange(righeOrdine);
            dbContext.SaveChanges();
        }

        if (demoOrdineIds.Count > 0)
        {
            var ordini = dbContext.Ordini.Where(o => demoOrdineIds.Contains(o.Id)).ToList();
            if (ordini.Count > 0)
            {
                dbContext.Ordini.RemoveRange(ordini);
                dbContext.SaveChanges();
            }
        }

        if (righe.Count > 0)
        {
            dbContext.DocumentiRighe.RemoveRange(righe);
            dbContext.SaveChanges();
        }

        if (demoDocumentoIds.Count > 0)
        {
            var documenti = dbContext.Documenti.Where(d => demoDocumentoIds.Contains(d.Id)).ToList();
            if (documenti.Count > 0)
            {
                dbContext.Documenti.RemoveRange(documenti);
                dbContext.SaveChanges();
            }
        }

        if (giacenze.Count > 0)
        {
            dbContext.GiacenzeMagazzino.RemoveRange(giacenze);
            dbContext.SaveChanges();
        }

        // ArticoliListino cascada su Articolo, ma la rimuoviamo esplicitamente per evitare vincoli residui.
        if (demoArticoloIds.Count > 0)
        {
            var listini = dbContext.ArticoliListino.Where(al => demoArticoloIds.Contains(al.ArticoloId)).ToList();
            if (listini.Count > 0)
            {
                dbContext.ArticoliListino.RemoveRange(listini);
                dbContext.SaveChanges();
            }
        }

        int articoliDeleted = 0;
        if (demoArticoloIds.Count > 0)
        {
            var articoli = dbContext.Articoli.Where(a => demoArticoloIds.Contains(a.Id)).ToList();
            articoliDeleted = articoli.Count;
            if (articoliDeleted > 0)
            {
                dbContext.Articoli.RemoveRange(articoli);
                dbContext.SaveChanges();
            }
        }

        int clientiDeleted = 0;
        if (demoClienteIds.Count > 0)
        {
            var clienti = dbContext.Clienti.Where(c => demoClienteIds.Contains(c.Id)).ToList();
            clientiDeleted = clienti.Count;
            if (clientiDeleted > 0)
            {
                dbContext.Clienti.RemoveRange(clienti);
                dbContext.SaveChanges();
            }
        }

        int fornitoriDeleted = 0;
        if (demoFornitoreIds.Count > 0)
        {
            var fornitori = dbContext.Fornitori.Where(f => demoFornitoreIds.Contains(f.Id)).ToList();
            fornitoriDeleted = fornitori.Count;
            if (fornitoriDeleted > 0)
            {
                dbContext.Fornitori.RemoveRange(fornitori);
                dbContext.SaveChanges();
            }
        }

        tx.Commit();

        return new DemoDataPurgeResult(
            Clienti: clientiDeleted,
            Fornitori: fornitoriDeleted,
            Articoli: articoliDeleted,
            Ordini: demoOrdineIds.Count,
            RigheOrdine: righeOrdine.Count,
            Documenti: demoDocumentoIds.Count,
            RigheDocumento: righe.Count,
            MovimentiMagazzino: movimenti.Count,
            GiacenzeMagazzino: giacenze.Count,
            Contratti: demoContrattoIds.Count,
            SchedeAssistenza: demoSchedaIds.Count,
            AssistenzaInterventi: demoAssistenzaInterventi.Count);
    }
}
