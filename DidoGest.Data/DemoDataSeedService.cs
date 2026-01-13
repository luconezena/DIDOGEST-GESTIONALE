using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using DidoGest.Core.Entities;

namespace DidoGest.Data;

public static class DemoDataSeedService
{
    public static void EnsureDemoData(DidoGestDbContext dbContext)
    {
        const string DemoPrefix = "DIDOGEST-DEMO";

        // Idempotente: crea solo ciò che manca.
        // Non deve mai cancellare/alterare dati reali.

        var nowLocal = DateTime.Now;
        var today = DateTime.Today;

        static decimal Round2(decimal v) => Math.Round(v, 2);

        static string NextOrdineNumero(DidoGestDbContext ctx, string tipo)
        {
            var prefix = tipo == "CLIENTE" ? "OC" : "OF";
            var last = ctx.Ordini.AsNoTracking()
                .Where(o => o.TipoOrdine == tipo)
                .OrderByDescending(o => o.Id)
                .FirstOrDefault();

            var numero = 1;
            if (last != null)
            {
                var digits = new string((last.NumeroOrdine ?? string.Empty).Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var n)) numero = n + 1;
            }

            return $"{prefix}{numero:D6}";
        }

        static DocumentoRiga MakeRiga(int documentoId, int numeroRiga, Articolo art, decimal quantita, decimal prezzoUnitario, string? note = null)
        {
            var imponibile = Round2(quantita * prezzoUnitario);
            var iva = Round2(imponibile * art.AliquotaIVA / 100m);
            var totale = imponibile + iva;

            return new DocumentoRiga
            {
                DocumentoId = documentoId,
                NumeroRiga = numeroRiga,
                ArticoloId = art.Id,
                Descrizione = art.Descrizione,
                Quantita = quantita,
                UnitaMisura = art.UnitaMisura,
                PrezzoUnitario = prezzoUnitario,
                Sconto1 = 0m,
                Sconto2 = 0m,
                Sconto3 = 0m,
                PrezzoNetto = prezzoUnitario,
                AliquotaIVA = art.AliquotaIVA,
                Imponibile = imponibile,
                ImportoIVA = iva,
                Totale = totale,
                RigaDescrittiva = false,
                Note = note
            };
        }

        // Listino BASE (utile anche per uso reale)
        var listinoBase = dbContext.Listini.FirstOrDefault(l => l.Codice == "BASE");
        if (listinoBase == null)
        {
            listinoBase = new Listino
            {
                Codice = "BASE",
                Descrizione = "Listino Base",
                DataInizioValidita = today,
                Attivo = true,
                DataCreazione = nowLocal
            };
            dbContext.Listini.Add(listinoBase);
            dbContext.SaveChanges();
        }

        // Magazzino demo MAG01: se non esistono magazzini, può essere principale, altrimenti no.
        var magDemo = dbContext.Magazzini.FirstOrDefault(m => m.Codice == "MAG01");
        if (magDemo == null)
        {
            var hasAny = dbContext.Magazzini.AsNoTracking().Any();
            var hasMain = dbContext.Magazzini.AsNoTracking().Any(m => m.Principale);

            magDemo = new Magazzino
            {
                Codice = "MAG01",
                Descrizione = "Magazzino Principale (DEMO)",
                Principale = !hasAny || !hasMain,
                Attivo = true,
                DataCreazione = nowLocal
            };
            dbContext.Magazzini.Add(magDemo);
            dbContext.SaveChanges();
        }

        // Agenti demo (per vedere subito la sezione popolata)
        var agente1 = dbContext.Agenti.FirstOrDefault(a => a.Codice == "DIDOGEST-DEMO-AG1");
        if (agente1 == null)
        {
            agente1 = new Agente
            {
                Codice = "DIDOGEST-DEMO-AG1",
                Nome = "Mario",
                Cognome = "Rossi",
                Telefono = "06-1111111",
                Email = "demo.agente1@example.com",
                PercentualeProvvigione = 5.00m,
                Attivo = true,
                DataCreazione = nowLocal,
                Note = DemoPrefix
            };
            dbContext.Agenti.Add(agente1);
            dbContext.SaveChanges();
        }

        var agente2 = dbContext.Agenti.FirstOrDefault(a => a.Codice == "DIDOGEST-DEMO-AG2");
        if (agente2 == null)
        {
            agente2 = new Agente
            {
                Codice = "DIDOGEST-DEMO-AG2",
                Nome = "Luigi",
                Cognome = "Bianchi",
                Telefono = "02-2222222",
                Email = "demo.agente2@example.com",
                PercentualeProvvigione = 7.50m,
                Attivo = true,
                DataCreazione = nowLocal,
                Note = DemoPrefix
            };
            dbContext.Agenti.Add(agente2);
            dbContext.SaveChanges();
        }

        // Cliente demo
        var cliente = dbContext.Clienti.FirstOrDefault(c => c.Codice == "DIDOGEST-DEMO-CLI");
        if (cliente == null)
        {
            cliente = new Cliente
            {
                Codice = "DIDOGEST-DEMO-CLI",
                RagioneSociale = "DIDOGEST DEMO - Cliente",
                Indirizzo = "Via Roma 1",
                CAP = "00100",
                Citta = "Roma",
                Provincia = "RM",
                Telefono = "06-0000000",
                Email = "demo.cliente@example.com",
                PEC = "demo.cliente@pec.example.com",
                CodiceSDI = "AAAAAA0",
                PartitaIVA = "00000000000",
                Attivo = true,
                DataCreazione = nowLocal,
                ListinoId = listinoBase.Id
            };
            dbContext.Clienti.Add(cliente);
            dbContext.SaveChanges();
        }

        // Assegna agente ai clienti demo (best effort, senza toccare dati reali)
        if (cliente.AgenteId == null)
        {
            cliente.AgenteId = agente1.Id;
            dbContext.SaveChanges();
        }

        // Secondo cliente demo (utile per vedere più anagrafiche e fattura FE)
        var cliente2 = dbContext.Clienti.FirstOrDefault(c => c.Codice == "DIDOGEST-DEMO-CLI2");
        if (cliente2 == null)
        {
            cliente2 = new Cliente
            {
                Codice = "DIDOGEST-DEMO-CLI2",
                RagioneSociale = "DIDOGEST DEMO - Cliente 2",
                Indirizzo = "Via Milano 10",
                CAP = "20100",
                Citta = "Milano",
                Provincia = "MI",
                Telefono = "02-0000000",
                Email = "demo.cliente2@example.com",
                PEC = "demo.cliente2@pec.example.com",
                CodiceSDI = "BBBBBB1",
                PartitaIVA = "11111111111",
                Attivo = true,
                DataCreazione = nowLocal,
                ListinoId = listinoBase.Id,
                Note = DemoPrefix
            };
            dbContext.Clienti.Add(cliente2);
            dbContext.SaveChanges();
        }

        if (cliente2.AgenteId == null)
        {
            cliente2.AgenteId = agente2.Id;
            dbContext.SaveChanges();
        }

        // Assistenza / Contratti demo (per popolare la sezione Assistenza)
        // Contratto demo
        var contrattoDemo = dbContext.Contratti.FirstOrDefault(c => c.NumeroContratto == "DIDOGEST-DEMO-CONTR01");
        if (contrattoDemo == null)
        {
            contrattoDemo = new Contratto
            {
                NumeroContratto = "DIDOGEST-DEMO-CONTR01",
                ClienteId = cliente.Id,
                Descrizione = "Contratto assistenza (DEMO)",
                DataInizio = today.AddMonths(-1),
                DataFine = today.AddMonths(11),
                TipoContratto = "MONTE_ORE",
                StatoContratto = "ATTIVO",
                MonteOreAcquistato = 10,
                MonteOreResiduo = 8,
                Importo = 500m,
                CostoOrarioExtra = 50m,
                Note = DemoPrefix,
                DataCreazione = nowLocal
            };
            dbContext.Contratti.Add(contrattoDemo);
            dbContext.SaveChanges();
        }

        // Scheda assistenza demo
        var schedaDemo = dbContext.SchedeAssistenza.FirstOrDefault(s => s.NumeroScheda == "DIDOGEST-DEMO-SA01");
        if (schedaDemo == null)
        {
            schedaDemo = new SchedaAssistenza
            {
                NumeroScheda = "DIDOGEST-DEMO-SA01",
                ClienteId = cliente.Id,
                DataApertura = today.AddDays(-7),
                StatoLavorazione = "IN_LAVORAZIONE",
                TecnicoAssegnato = "Tecnico DEMO",
                DescrizioneProdotto = "Stampante laser (DEMO)",
                Modello = "LaserJet X100 (DEMO)",
                Matricola = "SN-DEMO-001",
                DifettoDichiarato = "Non stampa correttamente (DEMO)",
                DifettoRiscontrato = "Toner quasi esaurito (DEMO)",
                InGaranzia = false,
                CostoLavorazione = 0m,
                CostoMateriali = 0m,
                TotaleIntervento = 0m,
                Note = DemoPrefix,
                DataCreazione = nowLocal,
                UtenteCreazione = "DEMO"
            };
            dbContext.SchedeAssistenza.Add(schedaDemo);
            dbContext.SaveChanges();
        }

        // Intervento demo collegato alla scheda
        var hasInterventoDemo = dbContext.AssistenzeInterventi.AsNoTracking().Any(i => i.SchedaAssistenzaId == schedaDemo.Id && i.Note != null && i.Note.Contains(DemoPrefix));
        if (!hasInterventoDemo)
        {
            var intervento = new AssistenzaIntervento
            {
                SchedaAssistenzaId = schedaDemo.Id,
                DataIntervento = today.AddDays(-6),
                Tecnico = "Tecnico DEMO",
                DescrizioneIntervento = "Sostituzione toner e pulizia rulli (DEMO)",
                MinutiLavorazione = 45,
                CostoOrario = 60m,
                TotaleLavorazione = Round2(0.75m * 60m),
                Note = DemoPrefix
            };
            dbContext.AssistenzeInterventi.Add(intervento);
            dbContext.SaveChanges();
        }

        // Fornitore demo
        var fornitore = dbContext.Fornitori.FirstOrDefault(f => f.Codice == "DIDOGEST-DEMO-FOR");
        if (fornitore == null)
        {
            fornitore = new Fornitore
            {
                Codice = "DIDOGEST-DEMO-FOR",
                RagioneSociale = "DIDOGEST DEMO - Fornitore",
                Indirizzo = "Via Industria 20",
                CAP = "35100",
                Citta = "Padova",
                Provincia = "PD",
                Telefono = "049-0000000",
                Email = "demo.fornitore@example.com",
                PartitaIVA = "22222222222",
                Attivo = true,
                DataCreazione = nowLocal
            };
            dbContext.Fornitori.Add(fornitore);
            dbContext.SaveChanges();
        }

        var fornitore2 = dbContext.Fornitori.FirstOrDefault(f => f.Codice == "DIDOGEST-DEMO-FOR2");
        if (fornitore2 == null)
        {
            fornitore2 = new Fornitore
            {
                Codice = "DIDOGEST-DEMO-FOR2",
                RagioneSociale = "DIDOGEST DEMO - Fornitore 2",
                Indirizzo = "Via Logistica 5",
                CAP = "40010",
                Citta = "Bologna",
                Provincia = "BO",
                Telefono = "051-0000000",
                Email = "demo.fornitore2@example.com",
                PartitaIVA = "33333333333",
                Attivo = true,
                DataCreazione = nowLocal,
                Note = DemoPrefix
            };
            dbContext.Fornitori.Add(fornitore2);
            dbContext.SaveChanges();
        }

        // Articolo demo
        var articolo = dbContext.Articoli.FirstOrDefault(a => a.Codice == "DIDOGEST-DEMO-ART");
        if (articolo == null)
        {
            articolo = new Articolo
            {
                Codice = "DIDOGEST-DEMO-ART",
                Descrizione = "DIDOGEST DEMO - Articolo",
                UnitaMisura = "PZ",
                PrezzoAcquisto = 60m,
                PrezzoVendita = 100m,
                AliquotaIVA = 22m,
                ScortaMinima = 10m,
                Attivo = true,
                DataCreazione = nowLocal,
                Categoria = "DEMO",
                Note = DemoPrefix,
                FornitorePredefinitoId = fornitore.Id
            };
            dbContext.Articoli.Add(articolo);
            dbContext.SaveChanges();
        }

        // Altri articoli demo (per scenari sottoscorta / servizi / prezzi diversi)
        Articolo EnsureArticolo(string codice, string descrizione, decimal pAcq, decimal pVen, decimal iva, decimal scortaMin, bool servizio)
        {
            var a = dbContext.Articoli.FirstOrDefault(x => x.Codice == codice);
            if (a != null) return a;

            a = new Articolo
            {
                Codice = codice,
                Descrizione = descrizione,
                UnitaMisura = servizio ? "NR" : "PZ",
                PrezzoAcquisto = pAcq,
                PrezzoVendita = pVen,
                AliquotaIVA = iva,
                ScortaMinima = scortaMin,
                ArticoloDiServizio = servizio,
                Attivo = true,
                DataCreazione = nowLocal,
                Categoria = "DEMO",
                Note = DemoPrefix,
                FornitorePredefinitoId = fornitore.Id
            };

            dbContext.Articoli.Add(a);
            dbContext.SaveChanges();
            return a;
        }

        var art2 = EnsureArticolo("DIDOGEST-DEMO-ART2", "DIDOGEST DEMO - Articolo 2 (Sottoscorta)", 15m, 29.90m, 22m, 25m, false);
        var art3 = EnsureArticolo("DIDOGEST-DEMO-ART3", "DIDOGEST DEMO - Articolo 3", 5m, 12.50m, 22m, 0m, false);
        var artServ = EnsureArticolo("DIDOGEST-DEMO-SRV", "DIDOGEST DEMO - Servizio (Manodopera)", 0m, 45m, 22m, 0m, true);
        var art4 = EnsureArticolo("DIDOGEST-DEMO-ART4", "DIDOGEST DEMO - Articolo 4", 80m, 130m, 22m, 5m, false);

        // Prezzi in listino BASE (best effort)
        void EnsurePrezzoListino(Articolo a, decimal prezzo)
        {
            var exists = dbContext.ArticoliListino.AsNoTracking().Any(x => x.ListinoId == listinoBase.Id && x.ArticoloId == a.Id);
            if (exists) return;
            dbContext.ArticoliListino.Add(new ArticoloListino
            {
                ListinoId = listinoBase.Id,
                ArticoloId = a.Id,
                Prezzo = prezzo,
                ScontoPercentuale = 0m,
                DataInizioValidita = today
            });
            dbContext.SaveChanges();
        }

        EnsurePrezzoListino(articolo, articolo.PrezzoVendita);
        EnsurePrezzoListino(art2, art2.PrezzoVendita);
        EnsurePrezzoListino(art3, art3.PrezzoVendita);
        EnsurePrezzoListino(art4, art4.PrezzoVendita);
        EnsurePrezzoListino(artServ, artServ.PrezzoVendita);

        // Giacenze demo (usate dalle viste Sottoscorta/Giacenze): idempotenti e non invasive.
        void EnsureGiacenza(Articolo a, decimal quantita, decimal impegnata = 0m)
        {
            var g = dbContext.GiacenzeMagazzino.FirstOrDefault(x => x.MagazzinoId == magDemo.Id && x.ArticoloId == a.Id);
            if (g == null)
            {
                dbContext.GiacenzeMagazzino.Add(new GiacenzaMagazzino
                {
                    MagazzinoId = magDemo.Id,
                    ArticoloId = a.Id,
                    Quantita = quantita,
                    QuantitaImpegnata = impegnata,
                    DataUltimoAggiornamento = nowLocal
                });
                dbContext.SaveChanges();
            }

            // Best effort: se non ci sono totali, inizializza.
            if (a.GiacenzaTotale <= 0m && quantita > 0m)
            {
                a.GiacenzaTotale = quantita;
                dbContext.SaveChanges();
            }

            // Movimento "carico" dimostrativo (non usato per calcoli, ma utile come storico)
            var hasCarico = dbContext.MovimentiMagazzino.AsNoTracking().Any(m =>
                m.NumeroDocumento == DemoPrefix &&
                m.Causale == "CARICO INIZIALE DEMO" &&
                m.ArticoloId == a.Id &&
                m.MagazzinoId == magDemo.Id);

            if (!hasCarico)
            {
                dbContext.MovimentiMagazzino.Add(new MovimentoMagazzino
                {
                    ArticoloId = a.Id,
                    MagazzinoId = magDemo.Id,
                    TipoMovimento = "CARICO",
                    Quantita = Math.Max(0m, quantita),
                    CostoUnitario = a.PrezzoAcquisto,
                    DataMovimento = today,
                    NumeroDocumento = DemoPrefix,
                    Causale = "CARICO INIZIALE DEMO",
                    Note = DemoPrefix
                });
                dbContext.SaveChanges();
            }
        }

        EnsureGiacenza(articolo, 50m);
        EnsureGiacenza(art2, 8m);          // sottoscorta rispetto a ScortaMinima 25
        EnsureGiacenza(art3, 120m);
        EnsureGiacenza(art4, 3m);          // sottoscorta rispetto a ScortaMinima 5
        EnsureGiacenza(artServ, 0m);

        // Documenti DEMO completi (Preventivo, DDT, Fatture + differita da più DDT)
        bool HasDemoDoc(string tipo, string marker) => dbContext.Documenti.AsNoTracking().Any(d =>
            d.TipoDocumento == tipo && d.Note != null && d.Note.Contains(marker));

        Documento CreateDocumento(string tipo, DateTime data, int magazzinoId, Cliente cli, string note)
        {
            var numero = DocumentNumberService.GenerateNumeroDocumento(dbContext, tipo, data);
            var doc = new Documento
            {
                TipoDocumento = tipo,
                NumeroDocumento = numero,
                DataDocumento = data,
                MagazzinoId = magazzinoId,
                ClienteId = cli.Id,
                RagioneSocialeDestinatario = cli.RagioneSociale,
                IndirizzoDestinatario = cli.Indirizzo,
                PartitaIVADestinatario = cli.PartitaIVA,
                CodiceFiscaleDestinatario = cli.CodiceFiscale,
                CodiceSDI = cli.CodiceSDI,
                PECDestinatario = cli.PEC,
                ScontoGlobale = 0m,
                SpeseAccessorie = 0m,
                Note = note,
                DataCreazione = nowLocal
            };
            dbContext.Documenti.Add(doc);
            dbContext.SaveChanges();
            return doc;
        }

        void FinalizeTotaliDocumento(int docId)
        {
            var doc = dbContext.Documenti.First(d => d.Id == docId);
            var righe = dbContext.DocumentiRighe.AsNoTracking().Where(r => r.DocumentoId == docId).ToList();
            doc.Imponibile = righe.Sum(r => r.Imponibile);
            doc.IVA = righe.Sum(r => r.ImportoIVA);
            doc.Totale = righe.Sum(r => r.Totale);
            doc.DataModifica = nowLocal;
            dbContext.SaveChanges();
        }

        // Preventivo con 3 righe (2 articoli + 1 servizio)
        if (!HasDemoDoc("PREVENTIVO", "DIDOGEST-DEMO - Preventivo"))
        {
            var prev = CreateDocumento("PREVENTIVO", today.AddDays(-15), magDemo.Id, cliente, "DIDOGEST-DEMO - Preventivo");

            dbContext.DocumentiRighe.Add(MakeRiga(prev.Id, 1, art2, 3m, art2.PrezzoVendita, DemoPrefix));
            dbContext.DocumentiRighe.Add(MakeRiga(prev.Id, 2, artServ, 2m, artServ.PrezzoVendita, DemoPrefix));
            dbContext.DocumentiRighe.Add(MakeRiga(prev.Id, 3, articolo, 1m, articolo.PrezzoVendita, DemoPrefix));
            dbContext.SaveChanges();
            FinalizeTotaliDocumento(prev.Id);
        }

        // DDT1 e DDT2 (per fattura differita)
        Documento? ddt1 = null;
        if (!HasDemoDoc("DDT", "DIDOGEST-DEMO - DDT 1"))
        {
            ddt1 = CreateDocumento("DDT", today.AddDays(-10), magDemo.Id, cliente, "DIDOGEST-DEMO - DDT 1");
            dbContext.DocumentiRighe.Add(MakeRiga(ddt1.Id, 1, articolo, 2m, articolo.PrezzoVendita, DemoPrefix));
            dbContext.DocumentiRighe.Add(MakeRiga(ddt1.Id, 2, art3, 5m, art3.PrezzoVendita, DemoPrefix));
            dbContext.SaveChanges();
            FinalizeTotaliDocumento(ddt1.Id);
        }
        else
        {
            ddt1 = dbContext.Documenti.AsNoTracking().FirstOrDefault(d => d.TipoDocumento == "DDT" && d.Note != null && d.Note.Contains("DIDOGEST-DEMO - DDT 1"));
        }

        Documento? ddt2 = null;
        if (!HasDemoDoc("DDT", "DIDOGEST-DEMO - DDT 2"))
        {
            ddt2 = CreateDocumento("DDT", today.AddDays(-7), magDemo.Id, cliente, "DIDOGEST-DEMO - DDT 2");
            dbContext.DocumentiRighe.Add(MakeRiga(ddt2.Id, 1, art4, 1m, art4.PrezzoVendita, DemoPrefix));
            dbContext.DocumentiRighe.Add(MakeRiga(ddt2.Id, 2, art2, 2m, art2.PrezzoVendita, DemoPrefix));
            dbContext.SaveChanges();
            FinalizeTotaliDocumento(ddt2.Id);
        }
        else
        {
            ddt2 = dbContext.Documenti.AsNoTracking().FirstOrDefault(d => d.TipoDocumento == "DDT" && d.Note != null && d.Note.Contains("DIDOGEST-DEMO - DDT 2"));
        }

        // Fattura differita da DDT1+DDT2 (con collegamenti)
        if (ddt1 != null && ddt2 != null && !HasDemoDoc("FATTURA", "DIDOGEST-DEMO - Fattura differita"))
        {
            var fatt = CreateDocumento("FATTURA", today.AddDays(-6), magDemo.Id, cliente, "DIDOGEST-DEMO - Fattura differita");
            fatt.ModalitaPagamento = "Bonifico";
            fatt.DataScadenzaPagamento = today.AddDays(30);

            // Copia righe (semplificata) dalle due bolle
            var righeOrig = dbContext.DocumentiRighe.AsNoTracking()
                .Where(r => r.DocumentoId == ddt1.Id || r.DocumentoId == ddt2.Id)
                .OrderBy(r => r.DocumentoId)
                .ThenBy(r => r.NumeroRiga)
                .ToList();

            var i = 1;
            foreach (var r in righeOrig)
            {
                // per semplicità, ricreiamo righe in fattura con stessi importi
                dbContext.DocumentiRighe.Add(new DocumentoRiga
                {
                    DocumentoId = fatt.Id,
                    NumeroRiga = i++,
                    ArticoloId = r.ArticoloId,
                    Descrizione = r.Descrizione,
                    Quantita = r.Quantita,
                    UnitaMisura = r.UnitaMisura,
                    PrezzoUnitario = r.PrezzoUnitario,
                    Sconto1 = 0m,
                    Sconto2 = 0m,
                    Sconto3 = 0m,
                    PrezzoNetto = r.PrezzoUnitario,
                    AliquotaIVA = r.AliquotaIVA,
                    Imponibile = r.Imponibile,
                    ImportoIVA = r.ImportoIVA,
                    Totale = r.Totale,
                    RigaDescrittiva = false,
                    Note = DemoPrefix
                });
            }

            dbContext.SaveChanges();
            FinalizeTotaliDocumento(fatt.Id);

            // Collegamenti
            void EnsureLink(int docId, int origineId)
            {
                var exists = dbContext.DocumentoCollegamenti.AsNoTracking().Any(x => x.DocumentoId == docId && x.DocumentoOrigineId == origineId);
                if (exists) return;
                dbContext.DocumentoCollegamenti.Add(new DocumentoCollegamento { DocumentoId = docId, DocumentoOrigineId = origineId });
                dbContext.SaveChanges();
            }

            EnsureLink(fatt.Id, ddt1.Id);
            EnsureLink(fatt.Id, ddt2.Id);
        }

        // Fattura immediata FE (cliente2)
        if (!HasDemoDoc("FATTURA", "DIDOGEST-DEMO - Fattura FE"))
        {
            var f = CreateDocumento("FATTURA", today.AddDays(-3), magDemo.Id, cliente2, "DIDOGEST-DEMO - Fattura FE");
            f.FatturaElettronica = true;
            f.StatoFatturaElettronica = "DA_INVIARE";
            f.ModalitaPagamento = "Carta";
            dbContext.SaveChanges();

            dbContext.DocumentiRighe.Add(MakeRiga(f.Id, 1, art3, 10m, art3.PrezzoVendita, DemoPrefix));
            dbContext.DocumentiRighe.Add(MakeRiga(f.Id, 2, artServ, 1m, artServ.PrezzoVendita, DemoPrefix));
            dbContext.SaveChanges();
            FinalizeTotaliDocumento(f.Id);
        }

        // Fattura accompagnatoria (esempio)
        if (!HasDemoDoc("FATTURA_ACCOMPAGNATORIA", "DIDOGEST-DEMO - Fattura accompagnatoria"))
        {
            var fa = CreateDocumento("FATTURA_ACCOMPAGNATORIA", today.AddDays(-1), magDemo.Id, cliente, "DIDOGEST-DEMO - Fattura accompagnatoria");
            dbContext.DocumentiRighe.Add(MakeRiga(fa.Id, 1, articolo, 1m, articolo.PrezzoVendita, DemoPrefix));
            dbContext.SaveChanges();
            FinalizeTotaliDocumento(fa.Id);
        }

        // Ordini DEMO (modulo Ordini)
        var hasOrdineClienteDemo = dbContext.Ordini.AsNoTracking().Any(o => o.TipoOrdine == "CLIENTE" && o.Note != null && o.Note.Contains(DemoPrefix));
        if (!hasOrdineClienteDemo)
        {
            var ordine = new Ordine
            {
                TipoOrdine = "CLIENTE",
                NumeroOrdine = NextOrdineNumero(dbContext, "CLIENTE"),
                DataOrdine = today.AddDays(-12),
                ClienteId = cliente.Id,
                StatoOrdine = "APERTO",
                Note = DemoPrefix,
                DataCreazione = nowLocal
            };
            dbContext.Ordini.Add(ordine);
            dbContext.SaveChanges();

            dbContext.OrdiniRighe.Add(new OrdineRiga
            {
                OrdineId = ordine.Id,
                NumeroRiga = 1,
                ArticoloId = art2.Id,
                Descrizione = art2.Descrizione,
                QuantitaOrdinata = 10m,
                QuantitaEvasa = 0m,
                UnitaMisura = art2.UnitaMisura,
                PrezzoUnitario = art2.PrezzoVendita,
                Sconto = 0m,
                AliquotaIVA = art2.AliquotaIVA,
                Totale = Round2(10m * art2.PrezzoVendita * (1m + art2.AliquotaIVA / 100m)),
                Note = DemoPrefix
            });

            dbContext.OrdiniRighe.Add(new OrdineRiga
            {
                OrdineId = ordine.Id,
                NumeroRiga = 2,
                ArticoloId = art4.Id,
                Descrizione = art4.Descrizione,
                QuantitaOrdinata = 2m,
                QuantitaEvasa = 0m,
                UnitaMisura = art4.UnitaMisura,
                PrezzoUnitario = art4.PrezzoVendita,
                Sconto = 0m,
                AliquotaIVA = art4.AliquotaIVA,
                Totale = Round2(2m * art4.PrezzoVendita * (1m + art4.AliquotaIVA / 100m)),
                Note = DemoPrefix
            });

            ordine.Imponibile = Round2(10m * art2.PrezzoVendita + 2m * art4.PrezzoVendita);
            ordine.IVA = Round2(ordine.Imponibile * 22m / 100m);
            ordine.Totale = ordine.Imponibile + ordine.IVA;
            dbContext.SaveChanges();
        }

        var hasOrdineFornitoreDemo = dbContext.Ordini.AsNoTracking().Any(o => o.TipoOrdine == "FORNITORE" && o.Note != null && o.Note.Contains(DemoPrefix));
        if (!hasOrdineFornitoreDemo)
        {
            var ordine = new Ordine
            {
                TipoOrdine = "FORNITORE",
                NumeroOrdine = NextOrdineNumero(dbContext, "FORNITORE"),
                DataOrdine = today.AddDays(-20),
                FornitoreId = fornitore.Id,
                StatoOrdine = "EVASO",
                Note = DemoPrefix,
                DataCreazione = nowLocal
            };
            dbContext.Ordini.Add(ordine);
            dbContext.SaveChanges();

            dbContext.OrdiniRighe.Add(new OrdineRiga
            {
                OrdineId = ordine.Id,
                NumeroRiga = 1,
                ArticoloId = articolo.Id,
                Descrizione = articolo.Descrizione,
                QuantitaOrdinata = 50m,
                QuantitaEvasa = 50m,
                UnitaMisura = articolo.UnitaMisura,
                PrezzoUnitario = articolo.PrezzoAcquisto,
                Sconto = 0m,
                AliquotaIVA = articolo.AliquotaIVA,
                Totale = Round2(50m * articolo.PrezzoAcquisto * (1m + articolo.AliquotaIVA / 100m)),
                Note = DemoPrefix
            });

            ordine.Imponibile = Round2(50m * articolo.PrezzoAcquisto);
            ordine.IVA = Round2(ordine.Imponibile * 22m / 100m);
            ordine.Totale = ordine.Imponibile + ordine.IVA;
            dbContext.SaveChanges();
        }
    }
}
