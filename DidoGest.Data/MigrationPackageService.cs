using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DidoGest.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data;

public sealed record MigrationPackageResult(
    int FilesWritten,
    int FilesRead,
    int Inserted,
    int Updated,
    int Skipped,
    int Errors)
{
    public static MigrationPackageResult Empty => new(0, 0, 0, 0, 0, 0);
}

/// <summary>
/// Export/Import “pacchetto migrazione” (cartella con più CSV) per trasferire dati da/verso DidoGest.
/// 
/// Note:
/// - Separator: ';'
/// - Encoding: UTF-8 con BOM
/// - Import: per le tabelle con chiave naturale (Codice/Numero) fa upsert; per le tabelle “evento” fa insert-only con dedup best-effort.
/// </summary>
public static class MigrationPackageService
{
    private const char Sep = ';';

    public static bool IsDatabaseProbablyEmpty()
    {
        using var ctx = DidoGestDb.CreateContext();

        // Heuristic: basta che una delle tabelle principali abbia dati.
        // (Non controlliamo tutte le tabelle per tenere l'operazione veloce.)
        return !(
            ctx.Clienti.Any() ||
            ctx.Fornitori.Any() ||
            ctx.Articoli.Any() ||
            ctx.Documenti.Any() ||
            ctx.Ordini.Any() ||
            ctx.MovimentiMagazzino.Any() ||
            ctx.RegistrazioniContabili.Any());
    }

    // Nominali file (prefisso numerico = ordine suggerito)
    private const string FileAgenti = "01_agenti.csv";
    private const string FileListini = "02_listini.csv";
    private const string FileMagazzini = "03_magazzini.csv";
    private const string FileFornitori = "04_fornitori.csv";
    private const string FileArticoli = "05_articoli.csv";
    private const string FileClienti = "06_clienti.csv";
    private const string FileArticoliListino = "07_articoli_listino.csv";

    private const string FileContratti = "10_contratti.csv";
    private const string FileCantieri = "11_cantieri.csv";
    private const string FileCantieriInterventi = "12_cantieri_interventi.csv";
    private const string FileSchedeAssistenza = "13_schede_assistenza.csv";
    private const string FileAssistenzaInterventi = "14_assistenza_interventi.csv";

    private const string FileOrdini = "20_ordini.csv";
    private const string FileOrdiniRighe = "21_ordini_righe.csv";

    private const string FileDocumenti = "30_documenti.csv";
    private const string FileDocumentiRighe = "31_documenti_righe.csv";
    private const string FileDocumentoCollegamenti = "32_documenti_collegamenti.csv";
    private const string FileMovimentiMagazzino = "33_movimenti_magazzino.csv";
    private const string FileDocumentiArchivio = "34_documenti_archivio.csv";

    private const string FilePianoDeiConti = "40_piano_dei_conti.csv";
    private const string FileRegistrazioni = "41_registrazioni_contabili.csv";
    private const string FileMovimentiContabili = "42_movimenti_contabili.csv";
    private const string FileRegistriIva = "43_registri_iva.csv";

    public static string ExportPackage(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Cartella di destinazione non valida.", nameof(targetDirectory));

        Directory.CreateDirectory(targetDirectory);

        using var ctx = DidoGestDb.CreateContext();

        // Caricamenti base
        var agenti = ctx.Agenti.AsNoTracking().OrderBy(a => a.Codice).ToList();
        var listini = ctx.Listini.AsNoTracking().OrderBy(l => l.Codice).ToList();
        var magazzini = ctx.Magazzini.AsNoTracking().OrderBy(m => m.Codice).ToList();
        var fornitori = ctx.Fornitori.AsNoTracking().OrderBy(f => f.Codice).ToList();
        var articoli = ctx.Articoli.AsNoTracking().OrderBy(a => a.Codice).ToList();

        // Clienti con relazioni
        var clienti = ctx.Clienti
            .AsNoTracking()
            .Include(c => c.Agente)
            .Include(c => c.Listino)
            .OrderBy(c => c.Codice)
            .ToList();

        // Prezzi listino
        var prezzi = ctx.ArticoliListino
            .AsNoTracking()
            .Include(x => x.Listino)
            .Include(x => x.Articolo)
            .OrderBy(x => x.ListinoId)
            .ThenBy(x => x.ArticoloId)
            .ThenBy(x => x.DataInizioValidita)
            .ToList();

        // Moduli extra
        var contratti = ctx.Contratti.AsNoTracking().OrderBy(c => c.NumeroContratto).ToList();
        var cantieri = ctx.Cantieri.AsNoTracking().OrderBy(c => c.CodiceCantiere).ToList();
        var cantieriInterventi = ctx.CantieriInterventi.AsNoTracking().OrderBy(i => i.CantiereId).ThenBy(i => i.DataIntervento).ToList();
        var schede = ctx.SchedeAssistenza.AsNoTracking().OrderBy(s => s.NumeroScheda).ToList();
        var assistenze = ctx.AssistenzeInterventi.AsNoTracking().OrderBy(i => i.SchedaAssistenzaId).ThenBy(i => i.DataIntervento).ToList();

        // Ordini
        var ordini = ctx.Ordini.AsNoTracking().OrderBy(o => o.TipoOrdine).ThenBy(o => o.NumeroOrdine).ToList();
        var ordiniRighe = ctx.OrdiniRighe.AsNoTracking().OrderBy(r => r.OrdineId).ThenBy(r => r.NumeroRiga).ToList();

        // Documenti
        var documenti = ctx.Documenti.AsNoTracking()
            .OrderBy(d => d.TipoDocumento)
            .ThenBy(d => d.NumeroDocumento)
            .ToList();

        var docRighe = ctx.DocumentiRighe.AsNoTracking().OrderBy(r => r.DocumentoId).ThenBy(r => r.NumeroRiga).ToList();
        var docColl = ctx.DocumentoCollegamenti.AsNoTracking().OrderBy(c => c.DocumentoId).ThenBy(c => c.DocumentoOrigineId).ToList();
        var movMag = ctx.MovimentiMagazzino.AsNoTracking().OrderBy(m => m.DataMovimento).ThenBy(m => m.Id).ToList();
        var docArch = ctx.DocumentiArchivio.AsNoTracking().OrderBy(d => d.NumeroProtocollo).ToList();

        // Contabilità
        var conti = ctx.PianiDeiConti.AsNoTracking().OrderBy(c => c.Codice).ToList();
        var regs = ctx.RegistrazioniContabili.AsNoTracking().OrderBy(r => r.DataRegistrazione).ThenBy(r => r.NumeroRegistrazione).ToList();
        var movCont = ctx.MovimentiContabili.AsNoTracking().OrderBy(m => m.RegistrazioneId).ThenBy(m => m.Id).ToList();
        var regIva = ctx.RegistriIVA.AsNoTracking().OrderBy(r => r.DataRegistrazione).ThenBy(r => r.NumeroProtocollo).ToList();

        var written = 0;

        WriteCsv(Path.Combine(targetDirectory, FileAgenti),
            new[] { "Codice", "Nome", "Cognome", "Telefono", "Cellulare", "Email", "PercentualeProvvigione", "Attivo", "Note" },
            agenti.Select(a => new[]
            {
                a.Codice,
                a.Nome,
                a.Cognome,
                a.Telefono,
                a.Cellulare,
                a.Email,
                FmtDec(a.PercentualeProvvigione),
                a.Attivo ? "1" : "0",
                a.Note
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileListini),
            new[] { "Codice", "Descrizione", "DataInizioValidita", "DataFineValidita", "Attivo", "Note" },
            listini.Select(l => new[]
            {
                l.Codice,
                l.Descrizione,
                FmtDate(l.DataInizioValidita),
                l.DataFineValidita.HasValue ? FmtDate(l.DataFineValidita.Value) : null,
                l.Attivo ? "1" : "0",
                l.Note
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileMagazzini),
            new[] { "Codice", "Descrizione", "Indirizzo", "Citta", "CAP", "Telefono", "Principale", "Attivo", "Note" },
            magazzini.Select(m => new[]
            {
                m.Codice,
                m.Descrizione,
                m.Indirizzo,
                m.Citta,
                m.CAP,
                m.Telefono,
                m.Principale ? "1" : "0",
                m.Attivo ? "1" : "0",
                m.Note
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileFornitori),
            new[] { "Codice", "RagioneSociale", "CodiceFiscale", "PartitaIVA", "Indirizzo", "CAP", "Citta", "Provincia", "Nazione", "Telefono", "Email", "PEC", "CodiceSDI", "GiorniPagamento", "Banca", "IBAN", "ValutazioneQualita", "DataUltimaValutazione", "Attivo", "Note" },
            fornitori.Select(f => new[]
            {
                f.Codice,
                f.RagioneSociale,
                f.CodiceFiscale,
                f.PartitaIVA,
                f.Indirizzo,
                f.CAP,
                f.Citta,
                f.Provincia,
                f.Nazione,
                f.Telefono,
                f.Email,
                f.PEC,
                f.CodiceSDI,
                f.GiorniPagamento?.ToString(CultureInfo.InvariantCulture),
                f.Banca,
                f.IBAN,
                FmtDec(f.ValutazioneQualita),
                f.DataUltimaValutazione.HasValue ? FmtDate(f.DataUltimaValutazione.Value) : null,
                f.Attivo ? "1" : "0",
                f.Note
            }));
        written++;

        // Articoli include fornitore predefinito come codice
        var fornitoreById = fornitori.ToDictionary(x => x.Id, x => x.Codice, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileArticoli),
            new[]
            {
                "Codice",
                "Descrizione",
                "DescrizioneEstesa",
                "CodiceEAN",
                "CodiceFornitori",
                "UnitaMisura",
                "PrezzoAcquisto",
                "PrezzoVendita",
                "AliquotaIVA",
                "ScortaMinima",
                "GestioneTaglie",
                "GestioneColori",
                "GestioneNumeriSerie",
                "GestioneLotti",
                "ArticoloDiServizio",
                "Attivo",
                "Categoria",
                "Sottocategoria",
                "Marca",
                "Peso",
                "Volume",
                "Note",
                "FornitorePredefinitoCodice"
            },
            articoli.Select(a => new[]
            {
                a.Codice,
                a.Descrizione,
                a.DescrizioneEstesa,
                a.CodiceEAN,
                a.CodiceFornitori,
                a.UnitaMisura,
                FmtDec(a.PrezzoAcquisto),
                FmtDec(a.PrezzoVendita),
                FmtDec(a.AliquotaIVA),
                FmtDec(a.ScortaMinima),
                a.GestioneTaglie ? "1" : "0",
                a.GestioneColori ? "1" : "0",
                a.GestioneNumeriSerie ? "1" : "0",
                a.GestioneLotti ? "1" : "0",
                a.ArticoloDiServizio ? "1" : "0",
                a.Attivo ? "1" : "0",
                a.Categoria,
                a.Sottocategoria,
                a.Marca,
                a.Peso.HasValue ? FmtDec(a.Peso.Value) : null,
                a.Volume.HasValue ? FmtDec(a.Volume.Value) : null,
                a.Note,
                a.FornitorePredefinitoId.HasValue && fornitoreById.TryGetValue(a.FornitorePredefinitoId.Value, out var fc) ? fc : null
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileClienti),
            new[]
            {
                "Codice",
                "RagioneSociale",
                "Nome",
                "Cognome",
                "CodiceFiscale",
                "PartitaIVA",
                "Indirizzo",
                "CAP",
                "Citta",
                "Provincia",
                "Nazione",
                "Telefono",
                "Cellulare",
                "Email",
                "PEC",
                "CodiceSDI",
                "FidoMassimo",
                "GiorniPagamento",
                "Banca",
                "IBAN",
                "Attivo",
                "Note",
                "AgenteCodice",
                "ListinoCodice"
            },
            clienti.Select(c => new[]
            {
                c.Codice,
                c.RagioneSociale,
                c.Nome,
                c.Cognome,
                c.CodiceFiscale,
                c.PartitaIVA,
                c.Indirizzo,
                c.CAP,
                c.Citta,
                c.Provincia,
                c.Nazione,
                c.Telefono,
                c.Cellulare,
                c.Email,
                c.PEC,
                c.CodiceSDI,
                FmtDec(c.FidoMassimo),
                c.GiorniPagamento?.ToString(CultureInfo.InvariantCulture),
                c.Banca,
                c.IBAN,
                c.Attivo ? "1" : "0",
                c.Note,
                c.Agente?.Codice,
                c.Listino?.Codice
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileArticoliListino),
            new[] { "ListinoCodice", "ArticoloCodice", "Prezzo", "ScontoPercentuale", "DataInizioValidita", "DataFineValidita" },
            prezzi.Select(p => new[]
            {
                p.Listino?.Codice,
                p.Articolo?.Codice,
                FmtDec(p.Prezzo),
                FmtDec(p.ScontoPercentuale),
                FmtDate(p.DataInizioValidita),
                p.DataFineValidita.HasValue ? FmtDate(p.DataFineValidita.Value) : null
            }));
        written++;

        // Contratti / Cantieri / Assistenza: referenze cliente via codice
        var clienteById = clienti.ToDictionary(x => x.Id, x => x.Codice, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileContratti),
            new[] { "NumeroContratto", "ClienteCodice", "Descrizione", "DataInizio", "DataFine", "Importo", "MonteOreAcquistato", "MonteOreResiduo", "CostoOrarioExtra", "TipoContratto", "StatoContratto", "FrequenzaFatturazione", "ProssimaFatturazione", "Note" },
            contratti.Select(c => new[]
            {
                c.NumeroContratto,
                clienteById.TryGetValue(c.ClienteId, out var cc) ? cc : null,
                c.Descrizione,
                FmtDate(c.DataInizio),
                c.DataFine.HasValue ? FmtDate(c.DataFine.Value) : null,
                FmtDec(c.Importo),
                c.MonteOreAcquistato?.ToString(CultureInfo.InvariantCulture),
                c.MonteOreResiduo?.ToString(CultureInfo.InvariantCulture),
                c.CostoOrarioExtra.HasValue ? FmtDec(c.CostoOrarioExtra.Value) : null,
                c.TipoContratto,
                c.StatoContratto,
                c.FrequenzaFatturazione,
                c.ProssimaFatturazione.HasValue ? FmtDate(c.ProssimaFatturazione.Value) : null,
                c.Note
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileCantieri),
            new[] { "CodiceCantiere", "ClienteCodice", "Descrizione", "Indirizzo", "Citta", "DataInizio", "DataFine", "ImportoPreventivato", "CostiSostenuti", "RicaviMaturati", "StatoCantiere", "ResponsabileCantiere", "Note" },
            cantieri.Select(c => new[]
            {
                c.CodiceCantiere,
                clienteById.TryGetValue(c.ClienteId, out var cc) ? cc : null,
                c.Descrizione,
                c.Indirizzo,
                c.Citta,
                FmtDate(c.DataInizio),
                c.DataFine.HasValue ? FmtDate(c.DataFine.Value) : null,
                FmtDec(c.ImportoPreventivato),
                FmtDec(c.CostiSostenuti),
                FmtDec(c.RicaviMaturati),
                c.StatoCantiere,
                c.ResponsabileCantiere,
                c.Note
            }));
        written++;

        var cantiereById = cantieri.ToDictionary(x => x.Id, x => x.CodiceCantiere, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileCantieriInterventi),
            new[] { "CantiereCodice", "DataIntervento", "Operai", "NumeroOperai", "OreManodopera", "CostoManodopera", "CostoMateriali", "TotaleCosto", "Descrizione", "Note" },
            cantieriInterventi.Select(i => new[]
            {
                cantiereById.TryGetValue(i.CantiereId, out var cod) ? cod : null,
                FmtDate(i.DataIntervento),
                i.Operai,
                i.NumeroOperai?.ToString(CultureInfo.InvariantCulture),
                i.OreManodopera?.ToString(CultureInfo.InvariantCulture),
                FmtDec(i.CostoManodopera),
                FmtDec(i.CostoMateriali),
                FmtDec(i.TotaleCosto),
                i.Descrizione,
                i.Note
            }));
        written++;

        // Schede assistenza + interventi (doc di carico/scarico esportati come docKey)
        var docKeyById = documenti.ToDictionary(d => d.Id, d => DocKey(d.TipoDocumento, d.NumeroDocumento), EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileSchedeAssistenza),
            new[] { "NumeroScheda", "DataApertura", "DataChiusura", "ClienteCodice", "DescrizioneProdotto", "Matricola", "Modello", "DifettoDichiarato", "DifettoRiscontrato", "InGaranzia", "StatoLavorazione", "TecnicoAssegnato", "CostoLavorazione", "CostoMateriali", "TotaleIntervento", "DocumentoCaricoKey", "DocumentoScaricoKey", "Note" },
            schede.Select(s => new[]
            {
                s.NumeroScheda,
                FmtDate(s.DataApertura),
                s.DataChiusura.HasValue ? FmtDate(s.DataChiusura.Value) : null,
                clienteById.TryGetValue(s.ClienteId, out var cc) ? cc : null,
                s.DescrizioneProdotto,
                s.Matricola,
                s.Modello,
                s.DifettoDichiarato,
                s.DifettoRiscontrato,
                s.InGaranzia ? "1" : "0",
                s.StatoLavorazione,
                s.TecnicoAssegnato,
                FmtDec(s.CostoLavorazione),
                FmtDec(s.CostoMateriali),
                FmtDec(s.TotaleIntervento),
                s.DocumentoCarico.HasValue && docKeyById.TryGetValue(s.DocumentoCarico.Value, out var dk1) ? dk1 : null,
                s.DocumentoScarico.HasValue && docKeyById.TryGetValue(s.DocumentoScarico.Value, out var dk2) ? dk2 : null,
                s.Note
            }));
        written++;

        var schedaById = schede.ToDictionary(x => x.Id, x => x.NumeroScheda, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileAssistenzaInterventi),
            new[] { "NumeroScheda", "DataIntervento", "Tecnico", "DescrizioneIntervento", "MinutiLavorazione", "CostoOrario", "TotaleLavorazione", "Note" },
            assistenze.Select(i => new[]
            {
                schedaById.TryGetValue(i.SchedaAssistenzaId, out var ns) ? ns : null,
                FmtDate(i.DataIntervento),
                i.Tecnico,
                i.DescrizioneIntervento,
                i.MinutiLavorazione?.ToString(CultureInfo.InvariantCulture),
                FmtDec(i.CostoOrario),
                FmtDec(i.TotaleLavorazione),
                i.Note
            }));
        written++;

        // Ordini: referenze cliente/fornitore via codice
        WriteCsv(Path.Combine(targetDirectory, FileOrdini),
            new[] { "TipoOrdine", "NumeroOrdine", "DataOrdine", "DataConsegnaPrevista", "ClienteCodice", "FornitoreCodice", "Imponibile", "IVA", "Totale", "StatoOrdine", "RiferimentoCliente", "Note" },
            ordini.Select(o => new[]
            {
                o.TipoOrdine,
                o.NumeroOrdine,
                FmtDate(o.DataOrdine),
                o.DataConsegnaPrevista.HasValue ? FmtDate(o.DataConsegnaPrevista.Value) : null,
                o.ClienteId.HasValue && clienteById.TryGetValue(o.ClienteId.Value, out var cco) ? cco : null,
                o.FornitoreId.HasValue && fornitoreById.TryGetValue(o.FornitoreId.Value, out var fco) ? fco : null,
                FmtDec(o.Imponibile),
                FmtDec(o.IVA),
                FmtDec(o.Totale),
                o.StatoOrdine,
                o.RiferimentoCliente,
                o.Note
            }));
        written++;

        var ordineKeyById = ordini.ToDictionary(x => x.Id, x => OrdKey(x.TipoOrdine, x.NumeroOrdine), EqualityComparer<int>.Default);
        var articoloById = articoli.ToDictionary(x => x.Id, x => x.Codice, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileOrdiniRighe),
            new[] { "OrdineKey", "NumeroRiga", "ArticoloCodice", "Descrizione", "QuantitaOrdinata", "QuantitaEvasa", "UnitaMisura", "PrezzoUnitario", "Sconto", "AliquotaIVA", "Totale", "Note" },
            ordiniRighe.Select(r => new[]
            {
                ordineKeyById.TryGetValue(r.OrdineId, out var ok) ? ok : null,
                r.NumeroRiga.ToString(CultureInfo.InvariantCulture),
                r.ArticoloId.HasValue && articoloById.TryGetValue(r.ArticoloId.Value, out var ac) ? ac : null,
                r.Descrizione,
                FmtDec(r.QuantitaOrdinata),
                FmtDec(r.QuantitaEvasa),
                r.UnitaMisura,
                FmtDec(r.PrezzoUnitario),
                FmtDec(r.Sconto),
                FmtDec(r.AliquotaIVA),
                FmtDec(r.Totale),
                r.Note
            }));
        written++;

        // Documenti: referenze cliente/fornitore/magazzino via codice
        var magazzinoById = magazzini.ToDictionary(x => x.Id, x => x.Codice, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileDocumenti),
            new[]
            {
                "TipoDocumento",
                "NumeroDocumento",
                "DataDocumento",
                "ClienteCodice",
                "FornitoreCodice",
                "RagioneSocialeDestinatario",
                "IndirizzoDestinatario",
                "Imponibile",
                "IVA",
                "Totale",
                "ScontoGlobale",
                "SpeseAccessorie",
                "ModalitaPagamento",
                "BancaAppoggio",
                "DataScadenzaPagamento",
                "Pagato",
                "DataPagamento",
                "PartitaIVADestinatario",
                "CodiceFiscaleDestinatario",
                "CodiceSDI",
                "PECDestinatario",
                "FatturaElettronica",
                "NomeFileXML",
                "XMLInviato",
                "DataInvioXML",
                "IdentificativoSDI",
                "StatoFatturaElettronica",
                "DocumentoOriginaleKey",
                "MagazzinoCodice",
                "CausaleDocumento",
                "AspettoBeni",
                "TrasportoCura",
                "Vettore",
                "NumeroColli",
                "Peso",
                "ReverseCharge",
                "SplitPayment",
                "Note",
                "UtenteCreazione"
            },
            documenti.Select(d => new[]
            {
                d.TipoDocumento,
                d.NumeroDocumento,
                FmtDate(d.DataDocumento),
                d.ClienteId.HasValue && clienteById.TryGetValue(d.ClienteId.Value, out var ccd) ? ccd : null,
                d.FornitoreId.HasValue && fornitoreById.TryGetValue(d.FornitoreId.Value, out var fcd) ? fcd : null,
                d.RagioneSocialeDestinatario,
                d.IndirizzoDestinatario,
                FmtDec(d.Imponibile),
                FmtDec(d.IVA),
                FmtDec(d.Totale),
                FmtDec(d.ScontoGlobale),
                FmtDec(d.SpeseAccessorie),
                d.ModalitaPagamento,
                d.BancaAppoggio,
                d.DataScadenzaPagamento.HasValue ? FmtDate(d.DataScadenzaPagamento.Value) : null,
                d.Pagato ? "1" : "0",
                d.DataPagamento.HasValue ? FmtDate(d.DataPagamento.Value) : null,
                d.PartitaIVADestinatario,
                d.CodiceFiscaleDestinatario,
                d.CodiceSDI,
                d.PECDestinatario,
                d.FatturaElettronica ? "1" : "0",
                d.NomeFileXML,
                d.XMLInviato ? "1" : "0",
                d.DataInvioXML.HasValue ? FmtDate(d.DataInvioXML.Value) : null,
                d.IdentificativoSDI,
                d.StatoFatturaElettronica,
                d.DocumentoOriginaleId.HasValue && docKeyById.TryGetValue(d.DocumentoOriginaleId.Value, out var dok) ? dok : null,
                magazzinoById.TryGetValue(d.MagazzinoId, out var mc) ? mc : null,
                d.CausaleDocumento,
                d.AspettoBeni,
                d.TrasportoCura,
                d.Vettore,
                d.NumeroColli?.ToString(CultureInfo.InvariantCulture),
                d.Peso.HasValue ? FmtDec(d.Peso.Value) : null,
                d.ReverseCharge ? "1" : "0",
                d.SplitPayment ? "1" : "0",
                d.Note,
                d.UtenteCreazione
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileDocumentiRighe),
            new[]
            {
                "DocumentoKey",
                "NumeroRiga",
                "ArticoloCodice",
                "Descrizione",
                "Quantita",
                "UnitaMisura",
                "PrezzoUnitario",
                "Sconto1",
                "Sconto2",
                "Sconto3",
                "PrezzoNetto",
                "AliquotaIVA",
                "Imponibile",
                "ImportoIVA",
                "Totale",
                "NumeroSerie",
                "Lotto",
                "RigaDescrittiva",
                "Note"
            },
            docRighe.Select(r => new[]
            {
                docKeyById.TryGetValue(r.DocumentoId, out var dk) ? dk : null,
                r.NumeroRiga.ToString(CultureInfo.InvariantCulture),
                r.ArticoloId.HasValue && articoloById.TryGetValue(r.ArticoloId.Value, out var ac) ? ac : null,
                r.Descrizione,
                FmtDec(r.Quantita),
                r.UnitaMisura,
                FmtDec(r.PrezzoUnitario),
                FmtDec(r.Sconto1),
                FmtDec(r.Sconto2),
                FmtDec(r.Sconto3),
                FmtDec(r.PrezzoNetto),
                FmtDec(r.AliquotaIVA),
                FmtDec(r.Imponibile),
                FmtDec(r.ImportoIVA),
                FmtDec(r.Totale),
                r.NumeroSerie,
                r.Lotto,
                r.RigaDescrittiva ? "1" : "0",
                r.Note
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileDocumentoCollegamenti),
            new[] { "DocumentoKey", "DocumentoOrigineKey" },
            docColl.Select(c => new[]
            {
                docKeyById.TryGetValue(c.DocumentoId, out var d1) ? d1 : null,
                docKeyById.TryGetValue(c.DocumentoOrigineId, out var d2) ? d2 : null
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileMovimentiMagazzino),
            new[] { "TipoMovimento", "DataMovimento", "ArticoloCodice", "MagazzinoCodice", "Quantita", "CostoUnitario", "NumeroDocumento", "DocumentoKey", "DocumentoRigaNumero", "NumeroSerie", "Lotto", "DataScadenza", "Causale", "Note", "UtenteCreazione" },
            movMag.Select(m => new[]
            {
                m.TipoMovimento,
                FmtDate(m.DataMovimento),
                articoloById.TryGetValue(m.ArticoloId, out var ac) ? ac : null,
                magazzinoById.TryGetValue(m.MagazzinoId, out var mc) ? mc : null,
                FmtDec(m.Quantita),
                FmtDec(m.CostoUnitario),
                m.NumeroDocumento,
                m.DocumentoId.HasValue && docKeyById.TryGetValue(m.DocumentoId.Value, out var dk) ? dk : null,
                m.DocumentoRigaId.HasValue && docRighe.FirstOrDefault(r => r.Id == m.DocumentoRigaId.Value) is { } rr
                    ? rr.NumeroRiga.ToString(CultureInfo.InvariantCulture)
                    : null,
                m.NumeroSerie,
                m.Lotto,
                m.DataScadenza.HasValue ? FmtDate(m.DataScadenza.Value) : null,
                m.Causale,
                m.Note,
                m.UtenteCreazione
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileDocumentiArchivio),
            new[] { "NumeroProtocollo", "DataProtocollo", "TitoloDocumento", "CategoriaDocumento", "Descrizione", "PercorsoFile", "EstensioneFile", "DimensioneFile", "ClienteCodice", "FornitoreCodice", "ArticoloCodice", "StatoDocumento", "DataApertura", "DataChiusura", "Tags", "Note" },
            docArch.Select(a => new[]
            {
                a.NumeroProtocollo,
                FmtDate(a.DataProtocollo),
                a.TitoloDocumento,
                a.CategoriaDocumento,
                a.Descrizione,
                a.PercorsoFile,
                a.EstensioneFile,
                a.DimensioneFile?.ToString(CultureInfo.InvariantCulture),
                a.ClienteId.HasValue && clienteById.TryGetValue(a.ClienteId.Value, out var cc) ? cc : null,
                a.FornitoreId.HasValue && fornitoreById.TryGetValue(a.FornitoreId.Value, out var fc) ? fc : null,
                a.ArticoloId.HasValue && articoloById.TryGetValue(a.ArticoloId.Value, out var ac) ? ac : null,
                a.StatoDocumento,
                a.DataApertura.HasValue ? FmtDate(a.DataApertura.Value) : null,
                a.DataChiusura.HasValue ? FmtDate(a.DataChiusura.Value) : null,
                a.Tags,
                a.Note
            }));
        written++;

        // Piano dei conti: esporta anche ContoSuperioreCodice
        var contoById = conti.ToDictionary(x => x.Id, x => x.Codice, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FilePianoDeiConti),
            new[] { "Codice", "Descrizione", "TipoConto", "ContoSuperioreCodice", "Livello", "ContoFoglia", "Attivo", "Note" },
            conti.Select(c => new[]
            {
                c.Codice,
                c.Descrizione,
                c.TipoConto,
                c.ContoSuperioreId.HasValue && contoById.TryGetValue(c.ContoSuperioreId.Value, out var sup) ? sup : null,
                c.Livello.ToString(CultureInfo.InvariantCulture),
                c.ContoFoglia ? "1" : "0",
                c.Attivo ? "1" : "0",
                c.Note
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileRegistrazioni),
            new[] { "NumeroRegistrazione", "DataRegistrazione", "CausaleContabile", "Descrizione", "DocumentoKey", "TotaleDare", "TotaleAvere", "UtenteCreazione" },
            regs.Select(r => new[]
            {
                r.NumeroRegistrazione,
                FmtDate(r.DataRegistrazione),
                r.CausaleContabile,
                r.Descrizione,
                r.DocumentoId.HasValue && docKeyById.TryGetValue(r.DocumentoId.Value, out var dk) ? dk : null,
                FmtDec(r.TotaleDare),
                FmtDec(r.TotaleAvere),
                r.UtenteCreazione
            }));
        written++;

        var regById = regs.ToDictionary(x => x.Id, x => x.NumeroRegistrazione, EqualityComparer<int>.Default);
        WriteCsv(Path.Combine(targetDirectory, FileMovimentiContabili),
            new[] { "NumeroRegistrazione", "ContoCodice", "ImportoDare", "ImportoAvere", "Descrizione" },
            movCont.Select(m => new[]
            {
                regById.TryGetValue(m.RegistrazioneId, out var nr) ? nr : null,
                contoById.TryGetValue(m.ContoId, out var cc) ? cc : null,
                FmtDec(m.ImportoDare),
                FmtDec(m.ImportoAvere),
                m.Descrizione
            }));
        written++;

        WriteCsv(Path.Combine(targetDirectory, FileRegistriIva),
            new[] { "TipoRegistro", "DataRegistrazione", "DocumentoKey", "NumeroProtocollo", "Imponibile", "AliquotaIVA", "ImportoIVA", "IVADetraibile", "IVAIndetraibile", "EsigibilitaDifferita", "DataEsigibilita", "Descrizione" },
            regIva.Select(r => new[]
            {
                r.TipoRegistro,
                FmtDate(r.DataRegistrazione),
                docKeyById.TryGetValue(r.DocumentoId, out var dk) ? dk : null,
                r.NumeroProtocollo,
                FmtDec(r.Imponibile),
                FmtDec(r.AliquotaIVA),
                FmtDec(r.ImportoIVA),
                FmtDec(r.IVADetraibile),
                FmtDec(r.IVAIndetraibile),
                r.EsigibilitaDifferita ? "1" : "0",
                r.DataEsigibilita.HasValue ? FmtDate(r.DataEsigibilita.Value) : null,
                r.Descrizione
            }));
        written++;

        return targetDirectory;
    }

    public static MigrationPackageResult ImportPackage(string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException("Cartella pacchetto migrazione non trovata.");

        using var ctx = DidoGestDb.CreateContext();

        var res = MigrationPackageResult.Empty;

        // Helper: upsert by code/numero
        // Step 1: Agenti
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileAgenti), rows =>
        {
            var byCode = ctx.Agenti.ToDictionary(a => a.Codice.Trim(), StringComparer.OrdinalIgnoreCase);
            var map = rows.Header;

            foreach (var row in rows.Records)
            {
                try
                {
                    var codice = rows.Get(row, "Codice");
                    if (string.IsNullOrWhiteSpace(codice)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byCode.TryGetValue(codice, out var a))
                    {
                        a = new Agente { Codice = codice };
                        ctx.Agenti.Add(a);
                        byCode[codice] = a;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "Nome"), v => a.Nome = v);
                    ApplyIf(rows.Get(row, "Cognome"), v => a.Cognome = v);
                    ApplyIf(rows.Get(row, "Telefono"), v => a.Telefono = v);
                    ApplyIf(rows.Get(row, "Cellulare"), v => a.Cellulare = v);
                    ApplyIf(rows.Get(row, "Email"), v => a.Email = v);
                    if (TryDec(rows.Get(row, "PercentualeProvvigione"), out var pv)) a.PercentualeProvvigione = pv;
                    if (TryBool(rows.Get(row, "Attivo"), out var att)) a.Attivo = att;
                    ApplyIf(rows.Get(row, "Note"), v => a.Note = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Step 2: Listini
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileListini), rows =>
        {
            var byCode = ctx.Listini.ToDictionary(l => l.Codice.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var codice = rows.Get(row, "Codice");
                    if (string.IsNullOrWhiteSpace(codice)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byCode.TryGetValue(codice, out var l))
                    {
                        l = new Listino { Codice = codice };
                        ctx.Listini.Add(l);
                        byCode[codice] = l;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "Descrizione"), v => l.Descrizione = v);
                    if (TryDate(rows.Get(row, "DataInizioValidita"), out var di)) l.DataInizioValidita = di;
                    if (TryDate(rows.Get(row, "DataFineValidita"), out var df)) l.DataFineValidita = df; else l.DataFineValidita = null;
                    if (TryBool(rows.Get(row, "Attivo"), out var att)) l.Attivo = att;
                    ApplyIf(rows.Get(row, "Note"), v => l.Note = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Step 3: Magazzini
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileMagazzini), rows =>
        {
            var byCode = ctx.Magazzini.ToDictionary(m => m.Codice.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var codice = rows.Get(row, "Codice");
                    if (string.IsNullOrWhiteSpace(codice)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byCode.TryGetValue(codice, out var m))
                    {
                        m = new Magazzino { Codice = codice };
                        ctx.Magazzini.Add(m);
                        byCode[codice] = m;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "Descrizione"), v => m.Descrizione = v);
                    ApplyIf(rows.Get(row, "Indirizzo"), v => m.Indirizzo = v);
                    ApplyIf(rows.Get(row, "Citta"), v => m.Citta = v);
                    ApplyIf(rows.Get(row, "CAP"), v => m.CAP = v);
                    ApplyIf(rows.Get(row, "Telefono"), v => m.Telefono = v);
                    if (TryBool(rows.Get(row, "Principale"), out var pr)) m.Principale = pr;
                    if (TryBool(rows.Get(row, "Attivo"), out var att)) m.Attivo = att;
                    ApplyIf(rows.Get(row, "Note"), v => m.Note = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            // sicurezza: garantiamo che ci sia almeno un magazzino principale
            if (!ctx.Magazzini.Any(x => x.Principale))
            {
                var first = ctx.Magazzini.OrderBy(x => x.Id).FirstOrDefault();
                if (first != null)
                    first.Principale = true;
            }

            ctx.SaveChanges();
        }) };

        // Preload lookups (post upsert basi)
        var agentiByCode = ctx.Agenti.ToDictionary(a => a.Codice.Trim(), a => a.Id, StringComparer.OrdinalIgnoreCase);
        var listiniByCode = ctx.Listini.ToDictionary(l => l.Codice.Trim(), l => l.Id, StringComparer.OrdinalIgnoreCase);
        var magazziniByCode = ctx.Magazzini.ToDictionary(m => m.Codice.Trim(), m => m.Id, StringComparer.OrdinalIgnoreCase);

        // Step 4: Fornitori
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileFornitori), rows =>
        {
            var byCode = ctx.Fornitori.ToDictionary(f => f.Codice.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var codice = rows.Get(row, "Codice");
                    if (string.IsNullOrWhiteSpace(codice)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byCode.TryGetValue(codice, out var f))
                    {
                        var rag = rows.Get(row, "RagioneSociale");
                        if (string.IsNullOrWhiteSpace(rag)) { res = res with { Errors = res.Errors + 1 }; continue; }

                        f = new Fornitore { Codice = codice, RagioneSociale = rag };
                        ctx.Fornitori.Add(f);
                        byCode[codice] = f;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "RagioneSociale"), v => f.RagioneSociale = v);
                    ApplyIf(rows.Get(row, "CodiceFiscale"), v => f.CodiceFiscale = v);
                    ApplyIf(rows.Get(row, "PartitaIVA"), v => f.PartitaIVA = v);
                    ApplyIf(rows.Get(row, "Indirizzo"), v => f.Indirizzo = v);
                    ApplyIf(rows.Get(row, "CAP"), v => f.CAP = v);
                    ApplyIf(rows.Get(row, "Citta"), v => f.Citta = v);
                    ApplyIf(rows.Get(row, "Provincia"), v => f.Provincia = v);
                    ApplyIf(rows.Get(row, "Nazione"), v => f.Nazione = v);
                    ApplyIf(rows.Get(row, "Telefono"), v => f.Telefono = v);
                    ApplyIf(rows.Get(row, "Email"), v => f.Email = v);
                    ApplyIf(rows.Get(row, "PEC"), v => f.PEC = v);
                    ApplyIf(rows.Get(row, "CodiceSDI"), v => f.CodiceSDI = v);
                    if (TryInt(rows.Get(row, "GiorniPagamento"), out var gp)) f.GiorniPagamento = gp;
                    ApplyIf(rows.Get(row, "Banca"), v => f.Banca = v);
                    ApplyIf(rows.Get(row, "IBAN"), v => f.IBAN = v);
                    if (TryDec(rows.Get(row, "ValutazioneQualita"), out var vq)) f.ValutazioneQualita = vq;
                    if (TryDate(rows.Get(row, "DataUltimaValutazione"), out var duv)) f.DataUltimaValutazione = duv;
                    if (TryBool(rows.Get(row, "Attivo"), out var att)) f.Attivo = att;
                    ApplyIf(rows.Get(row, "Note"), v => f.Note = v);
                    f.DataModifica = DateTime.Now;
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        var fornitoriByCode = ctx.Fornitori.ToDictionary(f => f.Codice.Trim(), f => f.Id, StringComparer.OrdinalIgnoreCase);

        // Step 5: Articoli
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileArticoli), rows =>
        {
            var byCode = ctx.Articoli.ToDictionary(a => a.Codice.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var codice = rows.Get(row, "Codice");
                    if (string.IsNullOrWhiteSpace(codice)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byCode.TryGetValue(codice, out var a))
                    {
                        var descr = rows.Get(row, "Descrizione");
                        if (string.IsNullOrWhiteSpace(descr)) { res = res with { Errors = res.Errors + 1 }; continue; }

                        a = new Articolo { Codice = codice, Descrizione = descr };
                        ctx.Articoli.Add(a);
                        byCode[codice] = a;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "Descrizione"), v => a.Descrizione = v);
                    ApplyIf(rows.Get(row, "DescrizioneEstesa"), v => a.DescrizioneEstesa = v);
                    ApplyIf(rows.Get(row, "CodiceEAN"), v => a.CodiceEAN = v);
                    ApplyIf(rows.Get(row, "CodiceFornitori"), v => a.CodiceFornitori = v);
                    ApplyIf(rows.Get(row, "UnitaMisura"), v => a.UnitaMisura = v);
                    if (TryDec(rows.Get(row, "PrezzoAcquisto"), out var pa)) a.PrezzoAcquisto = pa;
                    if (TryDec(rows.Get(row, "PrezzoVendita"), out var pv)) a.PrezzoVendita = pv;
                    if (TryDec(rows.Get(row, "AliquotaIVA"), out var iva)) a.AliquotaIVA = iva;
                    if (TryDec(rows.Get(row, "ScortaMinima"), out var sm)) a.ScortaMinima = sm;
                    if (TryBool(rows.Get(row, "GestioneTaglie"), out var bt)) a.GestioneTaglie = bt;
                    if (TryBool(rows.Get(row, "GestioneColori"), out var bc)) a.GestioneColori = bc;
                    if (TryBool(rows.Get(row, "GestioneNumeriSerie"), out var bns)) a.GestioneNumeriSerie = bns;
                    if (TryBool(rows.Get(row, "GestioneLotti"), out var bl)) a.GestioneLotti = bl;
                    if (TryBool(rows.Get(row, "ArticoloDiServizio"), out var bs)) a.ArticoloDiServizio = bs;
                    if (TryBool(rows.Get(row, "Attivo"), out var att)) a.Attivo = att;
                    ApplyIf(rows.Get(row, "Categoria"), v => a.Categoria = v);
                    ApplyIf(rows.Get(row, "Sottocategoria"), v => a.Sottocategoria = v);
                    ApplyIf(rows.Get(row, "Marca"), v => a.Marca = v);
                    if (TryDec(rows.Get(row, "Peso"), out var peso)) a.Peso = peso;
                    if (TryDec(rows.Get(row, "Volume"), out var vol)) a.Volume = vol;
                    ApplyIf(rows.Get(row, "Note"), v => a.Note = v);

                    var fornitoreCod = rows.Get(row, "FornitorePredefinitoCodice");
                    if (!string.IsNullOrWhiteSpace(fornitoreCod) && fornitoriByCode.TryGetValue(fornitoreCod, out var fid))
                        a.FornitorePredefinitoId = fid;

                    a.DataModifica = DateTime.Now;
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        var articoliByCode = ctx.Articoli.ToDictionary(a => a.Codice.Trim(), a => a.Id, StringComparer.OrdinalIgnoreCase);

        // Step 6: Clienti (con agente/listino)
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileClienti), rows =>
        {
            var byCode = ctx.Clienti.ToDictionary(c => c.Codice.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var codice = rows.Get(row, "Codice");
                    if (string.IsNullOrWhiteSpace(codice)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byCode.TryGetValue(codice, out var c))
                    {
                        var rag = rows.Get(row, "RagioneSociale");
                        if (string.IsNullOrWhiteSpace(rag)) { res = res with { Errors = res.Errors + 1 }; continue; }

                        c = new Cliente { Codice = codice, RagioneSociale = rag };
                        ctx.Clienti.Add(c);
                        byCode[codice] = c;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "RagioneSociale"), v => c.RagioneSociale = v);
                    ApplyIf(rows.Get(row, "Nome"), v => c.Nome = v);
                    ApplyIf(rows.Get(row, "Cognome"), v => c.Cognome = v);
                    ApplyIf(rows.Get(row, "CodiceFiscale"), v => c.CodiceFiscale = v);
                    ApplyIf(rows.Get(row, "PartitaIVA"), v => c.PartitaIVA = v);
                    ApplyIf(rows.Get(row, "Indirizzo"), v => c.Indirizzo = v);
                    ApplyIf(rows.Get(row, "CAP"), v => c.CAP = v);
                    ApplyIf(rows.Get(row, "Citta"), v => c.Citta = v);
                    ApplyIf(rows.Get(row, "Provincia"), v => c.Provincia = v);
                    ApplyIf(rows.Get(row, "Nazione"), v => c.Nazione = v);
                    ApplyIf(rows.Get(row, "Telefono"), v => c.Telefono = v);
                    ApplyIf(rows.Get(row, "Cellulare"), v => c.Cellulare = v);
                    ApplyIf(rows.Get(row, "Email"), v => c.Email = v);
                    ApplyIf(rows.Get(row, "PEC"), v => c.PEC = v);
                    ApplyIf(rows.Get(row, "CodiceSDI"), v => c.CodiceSDI = v);
                    if (TryDec(rows.Get(row, "FidoMassimo"), out var fido)) c.FidoMassimo = fido;
                    if (TryInt(rows.Get(row, "GiorniPagamento"), out var gp)) c.GiorniPagamento = gp;
                    ApplyIf(rows.Get(row, "Banca"), v => c.Banca = v);
                    ApplyIf(rows.Get(row, "IBAN"), v => c.IBAN = v);
                    if (TryBool(rows.Get(row, "Attivo"), out var att)) c.Attivo = att;
                    ApplyIf(rows.Get(row, "Note"), v => c.Note = v);

                    var agenteCod = rows.Get(row, "AgenteCodice");
                    if (!string.IsNullOrWhiteSpace(agenteCod) && agentiByCode.TryGetValue(agenteCod, out var aid))
                        c.AgenteId = aid;

                    var listinoCod = rows.Get(row, "ListinoCodice");
                    if (!string.IsNullOrWhiteSpace(listinoCod) && listiniByCode.TryGetValue(listinoCod, out var lid))
                        c.ListinoId = lid;

                    c.DataModifica = DateTime.Now;
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        var clientiByCode = ctx.Clienti.ToDictionary(c => c.Codice.Trim(), c => c.Id, StringComparer.OrdinalIgnoreCase);

        // Step 7: ArticoliListino (key: listino+articolo+dataInizio)
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileArticoliListino), rows =>
        {
            var existing = ctx.ArticoliListino
                .AsNoTracking()
                .Select(x => new { x.Id, x.ListinoId, x.ArticoloId, x.DataInizioValidita })
                .ToList();

            var set = new HashSet<string>(existing.Select(x => PriceKey(x.ListinoId, x.ArticoloId, x.DataInizioValidita)), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var listinoCod = rows.Get(row, "ListinoCodice");
                    var articoloCod = rows.Get(row, "ArticoloCodice");
                    if (string.IsNullOrWhiteSpace(listinoCod) || string.IsNullOrWhiteSpace(articoloCod)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!listiniByCode.TryGetValue(listinoCod, out var lid)) { res = res with { Errors = res.Errors + 1 }; continue; }
                    if (!articoliByCode.TryGetValue(articoloCod, out var aid)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    if (!TryDate(rows.Get(row, "DataInizioValidita"), out var di)) di = DateTime.Today;

                    var key = PriceKey(lid, aid, di);
                    if (set.Contains(key))
                    {
                        // insert-only: evitiamo modifiche di storico
                        res = res with { Skipped = res.Skipped + 1 };
                        continue;
                    }

                    var p = new ArticoloListino
                    {
                        ListinoId = lid,
                        ArticoloId = aid,
                        DataInizioValidita = di
                    };

                    if (TryDec(rows.Get(row, "Prezzo"), out var prezzo)) p.Prezzo = prezzo;
                    if (TryDec(rows.Get(row, "ScontoPercentuale"), out var sconto)) p.ScontoPercentuale = sconto;
                    if (TryDate(rows.Get(row, "DataFineValidita"), out var df)) p.DataFineValidita = df;

                    ctx.ArticoliListino.Add(p);
                    set.Add(key);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Step 10: Contratti
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileContratti), rows =>
        {
            var byNum = ctx.Contratti.ToDictionary(c => c.NumeroContratto.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var num = rows.Get(row, "NumeroContratto");
                    var clienteCod = rows.Get(row, "ClienteCodice");
                    if (string.IsNullOrWhiteSpace(num) || string.IsNullOrWhiteSpace(clienteCod)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!clientiByCode.TryGetValue(clienteCod, out var cid)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    if (!byNum.TryGetValue(num, out var c))
                    {
                        c = new Contratto { NumeroContratto = num, ClienteId = cid };
                        ctx.Contratti.Add(c);
                        byNum[num] = c;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        c.ClienteId = cid;
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "Descrizione"), v => c.Descrizione = v);
                    if (TryDate(rows.Get(row, "DataInizio"), out var di)) c.DataInizio = di;
                    if (TryDate(rows.Get(row, "DataFine"), out var df)) c.DataFine = df; else c.DataFine = null;
                    if (TryDec(rows.Get(row, "Importo"), out var imp)) c.Importo = imp;
                    if (TryInt(rows.Get(row, "MonteOreAcquistato"), out var moa)) c.MonteOreAcquistato = moa;
                    if (TryInt(rows.Get(row, "MonteOreResiduo"), out var mor)) c.MonteOreResiduo = mor;
                    if (TryDec(rows.Get(row, "CostoOrarioExtra"), out var coe)) c.CostoOrarioExtra = coe;
                    ApplyIf(rows.Get(row, "TipoContratto"), v => c.TipoContratto = v);
                    ApplyIf(rows.Get(row, "StatoContratto"), v => c.StatoContratto = v);
                    ApplyIf(rows.Get(row, "FrequenzaFatturazione"), v => c.FrequenzaFatturazione = v);
                    if (TryDate(rows.Get(row, "ProssimaFatturazione"), out var pf)) c.ProssimaFatturazione = pf;
                    ApplyIf(rows.Get(row, "Note"), v => c.Note = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Step 11: Cantieri
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileCantieri), rows =>
        {
            var byCod = ctx.Cantieri.ToDictionary(c => c.CodiceCantiere.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var cod = rows.Get(row, "CodiceCantiere");
                    var clienteCod = rows.Get(row, "ClienteCodice");
                    if (string.IsNullOrWhiteSpace(cod) || string.IsNullOrWhiteSpace(clienteCod)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!clientiByCode.TryGetValue(clienteCod, out var cid)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    if (!byCod.TryGetValue(cod, out var c))
                    {
                        c = new Cantiere { CodiceCantiere = cod, ClienteId = cid };
                        ctx.Cantieri.Add(c);
                        byCod[cod] = c;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        c.ClienteId = cid;
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "Descrizione"), v => c.Descrizione = v);
                    ApplyIf(rows.Get(row, "Indirizzo"), v => c.Indirizzo = v);
                    ApplyIf(rows.Get(row, "Citta"), v => c.Citta = v);
                    if (TryDate(rows.Get(row, "DataInizio"), out var di)) c.DataInizio = di;
                    if (TryDate(rows.Get(row, "DataFine"), out var df)) c.DataFine = df; else c.DataFine = null;
                    if (TryDec(rows.Get(row, "ImportoPreventivato"), out var ip)) c.ImportoPreventivato = ip;
                    if (TryDec(rows.Get(row, "CostiSostenuti"), out var cs)) c.CostiSostenuti = cs;
                    if (TryDec(rows.Get(row, "RicaviMaturati"), out var rm)) c.RicaviMaturati = rm;
                    ApplyIf(rows.Get(row, "StatoCantiere"), v => c.StatoCantiere = v);
                    ApplyIf(rows.Get(row, "ResponsabileCantiere"), v => c.ResponsabileCantiere = v);
                    ApplyIf(rows.Get(row, "Note"), v => c.Note = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        var cantieriByCode = ctx.Cantieri.ToDictionary(c => c.CodiceCantiere.Trim(), c => c.Id, StringComparer.OrdinalIgnoreCase);

        // Step 12: CantieriInterventi (insert-only dedup best-effort)
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileCantieriInterventi), rows =>
        {
            var existing = ctx.CantieriInterventi.AsNoTracking().Select(i => new { i.CantiereId, i.DataIntervento, i.Descrizione, i.Operai, i.OreManodopera, i.TotaleCosto }).ToList();
            var set = new HashSet<string>(existing.Select(x => $"{x.CantiereId}|{x.DataIntervento:O}|{x.Descrizione}|{x.Operai}|{x.OreManodopera}|{x.TotaleCosto.ToString(CultureInfo.InvariantCulture)}"), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var cod = rows.Get(row, "CantiereCodice");
                    if (string.IsNullOrWhiteSpace(cod) || !cantieriByCode.TryGetValue(cod, out var cid)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!TryDate(rows.Get(row, "DataIntervento"), out var di)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    var descr = rows.Get(row, "Descrizione");
                    var operai = rows.Get(row, "Operai");
                    var ore = rows.Get(row, "OreManodopera");
                    var tot = rows.Get(row, "TotaleCosto");

                    var key = $"{cid}|{di:O}|{descr}|{operai}|{ore}|{tot}";
                    if (set.Contains(key)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    var i = new CantiereIntervento
                    {
                        CantiereId = cid,
                        DataIntervento = di,
                        Operai = operai,
                        Descrizione = descr,
                        Note = rows.Get(row, "Note")
                    };

                    if (TryInt(rows.Get(row, "NumeroOperai"), out var no)) i.NumeroOperai = no;
                    if (TryInt(rows.Get(row, "OreManodopera"), out var om)) i.OreManodopera = om;
                    if (TryDec(rows.Get(row, "CostoManodopera"), out var cm)) i.CostoManodopera = cm;
                    if (TryDec(rows.Get(row, "CostoMateriali"), out var cmat)) i.CostoMateriali = cmat;
                    if (TryDec(rows.Get(row, "TotaleCosto"), out var tc)) i.TotaleCosto = tc;

                    ctx.CantieriInterventi.Add(i);
                    set.Add(key);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Step 13-14: Assistenza
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileSchedeAssistenza), rows =>
        {
            var byNum = ctx.SchedeAssistenza.ToDictionary(s => s.NumeroScheda.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var num = rows.Get(row, "NumeroScheda");
                    var clienteCod = rows.Get(row, "ClienteCodice");
                    if (string.IsNullOrWhiteSpace(num) || string.IsNullOrWhiteSpace(clienteCod)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!clientiByCode.TryGetValue(clienteCod, out var cid)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    if (!byNum.TryGetValue(num, out var s))
                    {
                        s = new SchedaAssistenza { NumeroScheda = num, ClienteId = cid };
                        ctx.SchedeAssistenza.Add(s);
                        byNum[num] = s;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        s.ClienteId = cid;
                        res = res with { Updated = res.Updated + 1 };
                    }

                    if (TryDate(rows.Get(row, "DataApertura"), out var da)) s.DataApertura = da;
                    if (TryDate(rows.Get(row, "DataChiusura"), out var dc)) s.DataChiusura = dc; else s.DataChiusura = null;
                    ApplyIf(rows.Get(row, "DescrizioneProdotto"), v => s.DescrizioneProdotto = v);
                    ApplyIf(rows.Get(row, "Matricola"), v => s.Matricola = v);
                    ApplyIf(rows.Get(row, "Modello"), v => s.Modello = v);
                    ApplyIf(rows.Get(row, "DifettoDichiarato"), v => s.DifettoDichiarato = v);
                    ApplyIf(rows.Get(row, "DifettoRiscontrato"), v => s.DifettoRiscontrato = v);
                    if (TryBool(rows.Get(row, "InGaranzia"), out var ig)) s.InGaranzia = ig;
                    ApplyIf(rows.Get(row, "StatoLavorazione"), v => s.StatoLavorazione = v);
                    ApplyIf(rows.Get(row, "TecnicoAssegnato"), v => s.TecnicoAssegnato = v);
                    if (TryDec(rows.Get(row, "CostoLavorazione"), out var cl)) s.CostoLavorazione = cl;
                    if (TryDec(rows.Get(row, "CostoMateriali"), out var cm)) s.CostoMateriali = cm;
                    if (TryDec(rows.Get(row, "TotaleIntervento"), out var ti)) s.TotaleIntervento = ti;
                    ApplyIf(rows.Get(row, "Note"), v => s.Note = v);

                    // DocumentoCarico/Scarico: risolviamo dopo aver importato i documenti (se presenti)
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        var schedeByNum = ctx.SchedeAssistenza.ToDictionary(s => s.NumeroScheda.Trim(), s => s.Id, StringComparer.OrdinalIgnoreCase);

        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileAssistenzaInterventi), rows =>
        {
            var existing = ctx.AssistenzeInterventi.AsNoTracking().Select(i => new { i.SchedaAssistenzaId, i.DataIntervento, i.Tecnico, i.DescrizioneIntervento, i.TotaleLavorazione }).ToList();
            var set = new HashSet<string>(existing.Select(x => $"{x.SchedaAssistenzaId}|{x.DataIntervento:O}|{x.Tecnico}|{x.DescrizioneIntervento}|{x.TotaleLavorazione.ToString(CultureInfo.InvariantCulture)}"), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var num = rows.Get(row, "NumeroScheda");
                    if (string.IsNullOrWhiteSpace(num) || !schedeByNum.TryGetValue(num, out var sid)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!TryDate(rows.Get(row, "DataIntervento"), out var di)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    var tecnico = rows.Get(row, "Tecnico");
                    var descr = rows.Get(row, "DescrizioneIntervento");
                    var tot = rows.Get(row, "TotaleLavorazione");

                    var key = $"{sid}|{di:O}|{tecnico}|{descr}|{tot}";
                    if (set.Contains(key)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    var i = new AssistenzaIntervento
                    {
                        SchedaAssistenzaId = sid,
                        DataIntervento = di,
                        Tecnico = tecnico,
                        DescrizioneIntervento = descr,
                        Note = rows.Get(row, "Note")
                    };

                    if (TryInt(rows.Get(row, "MinutiLavorazione"), out var ml)) i.MinutiLavorazione = ml;
                    if (TryDec(rows.Get(row, "CostoOrario"), out var co)) i.CostoOrario = co;
                    if (TryDec(rows.Get(row, "TotaleLavorazione"), out var tl)) i.TotaleLavorazione = tl;

                    ctx.AssistenzeInterventi.Add(i);
                    set.Add(key);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Step 20-21: Ordini
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileOrdini), rows =>
        {
            var byKey = ctx.Ordini.ToDictionary(o => OrdKey(o.TipoOrdine, o.NumeroOrdine), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var tipo = rows.Get(row, "TipoOrdine");
                    var num = rows.Get(row, "NumeroOrdine");
                    if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(num)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    var key = OrdKey(tipo, num);
                    if (!byKey.TryGetValue(key, out var o))
                    {
                        o = new Ordine { TipoOrdine = tipo, NumeroOrdine = num };
                        ctx.Ordini.Add(o);
                        byKey[key] = o;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    if (TryDate(rows.Get(row, "DataOrdine"), out var dd)) o.DataOrdine = dd;
                    if (TryDate(rows.Get(row, "DataConsegnaPrevista"), out var dcp)) o.DataConsegnaPrevista = dcp; else o.DataConsegnaPrevista = null;

                    var clienteCod = rows.Get(row, "ClienteCodice");
                    var fornitoreCod = rows.Get(row, "FornitoreCodice");
                    if (!string.IsNullOrWhiteSpace(clienteCod) && clientiByCode.TryGetValue(clienteCod, out var cid)) o.ClienteId = cid;
                    if (!string.IsNullOrWhiteSpace(fornitoreCod) && fornitoriByCode.TryGetValue(fornitoreCod, out var fid)) o.FornitoreId = fid;

                    if (TryDec(rows.Get(row, "Imponibile"), out var imp)) o.Imponibile = imp;
                    if (TryDec(rows.Get(row, "IVA"), out var iva)) o.IVA = iva;
                    if (TryDec(rows.Get(row, "Totale"), out var tot)) o.Totale = tot;
                    ApplyIf(rows.Get(row, "StatoOrdine"), v => o.StatoOrdine = v);
                    ApplyIf(rows.Get(row, "RiferimentoCliente"), v => o.RiferimentoCliente = v);
                    ApplyIf(rows.Get(row, "Note"), v => o.Note = v);
                    o.DataModifica = DateTime.Now;
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        var ordiniByKeyToId = ctx.Ordini.ToDictionary(o => OrdKey(o.TipoOrdine, o.NumeroOrdine), o => o.Id, StringComparer.OrdinalIgnoreCase);

        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileOrdiniRighe), rows =>
        {
            var existing = ctx.OrdiniRighe.AsNoTracking().Select(r => new { r.OrdineId, r.NumeroRiga, r.Descrizione, r.QuantitaOrdinata, r.PrezzoUnitario, r.Totale }).ToList();
            var set = new HashSet<string>(existing.Select(x => $"{x.OrdineId}|{x.NumeroRiga}|{x.Descrizione}|{x.QuantitaOrdinata.ToString(CultureInfo.InvariantCulture)}|{x.PrezzoUnitario.ToString(CultureInfo.InvariantCulture)}|{x.Totale.ToString(CultureInfo.InvariantCulture)}"), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var ordineKey = rows.Get(row, "OrdineKey");
                    if (string.IsNullOrWhiteSpace(ordineKey) || !ordiniByKeyToId.TryGetValue(ordineKey, out var oid)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!TryInt(rows.Get(row, "NumeroRiga"), out var nr)) nr = 0;
                    var descr = rows.Get(row, "Descrizione") ?? string.Empty;
                    var q = rows.Get(row, "QuantitaOrdinata") ?? string.Empty;
                    var pu = rows.Get(row, "PrezzoUnitario") ?? string.Empty;
                    var tot = rows.Get(row, "Totale") ?? string.Empty;

                    var key = $"{oid}|{nr}|{descr}|{q}|{pu}|{tot}";
                    if (set.Contains(key)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    var r = new OrdineRiga
                    {
                        OrdineId = oid,
                        NumeroRiga = nr,
                        Descrizione = descr,
                        Note = rows.Get(row, "Note")
                    };

                    var articoloCod = rows.Get(row, "ArticoloCodice");
                    if (!string.IsNullOrWhiteSpace(articoloCod) && articoliByCode.TryGetValue(articoloCod, out var aid)) r.ArticoloId = aid;

                    if (TryDec(rows.Get(row, "QuantitaOrdinata"), out var qo)) r.QuantitaOrdinata = qo;
                    if (TryDec(rows.Get(row, "QuantitaEvasa"), out var qe)) r.QuantitaEvasa = qe;
                    ApplyIf(rows.Get(row, "UnitaMisura"), v => r.UnitaMisura = v);
                    if (TryDec(rows.Get(row, "PrezzoUnitario"), out var punit)) r.PrezzoUnitario = punit;
                    if (TryDec(rows.Get(row, "Sconto"), out var sc)) r.Sconto = sc;
                    if (TryDec(rows.Get(row, "AliquotaIVA"), out var iva)) r.AliquotaIVA = iva;
                    if (TryDec(rows.Get(row, "Totale"), out var t)) r.Totale = t;

                    ctx.OrdiniRighe.Add(r);
                    set.Add(key);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Step 30-34: Documenti
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileDocumenti), rows =>
        {
            var byKey = ctx.Documenti.ToDictionary(d => DocKey(d.TipoDocumento, d.NumeroDocumento), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var tipo = rows.Get(row, "TipoDocumento");
                    var num = rows.Get(row, "NumeroDocumento");
                    if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(num)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    var key = DocKey(tipo, num);
                    if (!byKey.TryGetValue(key, out var d))
                    {
                        d = new Documento { TipoDocumento = tipo, NumeroDocumento = num };
                        ctx.Documenti.Add(d);
                        byKey[key] = d;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    if (TryDate(rows.Get(row, "DataDocumento"), out var dd)) d.DataDocumento = dd;

                    var clienteCod = rows.Get(row, "ClienteCodice");
                    var fornitoreCod = rows.Get(row, "FornitoreCodice");
                    if (!string.IsNullOrWhiteSpace(clienteCod) && clientiByCode.TryGetValue(clienteCod, out var cid)) d.ClienteId = cid;
                    if (!string.IsNullOrWhiteSpace(fornitoreCod) && fornitoriByCode.TryGetValue(fornitoreCod, out var fid)) d.FornitoreId = fid;

                    ApplyIf(rows.Get(row, "RagioneSocialeDestinatario"), v => d.RagioneSocialeDestinatario = v);
                    ApplyIf(rows.Get(row, "IndirizzoDestinatario"), v => d.IndirizzoDestinatario = v);

                    if (TryDec(rows.Get(row, "Imponibile"), out var imp)) d.Imponibile = imp;
                    if (TryDec(rows.Get(row, "IVA"), out var iva)) d.IVA = iva;
                    if (TryDec(rows.Get(row, "Totale"), out var tot)) d.Totale = tot;
                    if (TryDec(rows.Get(row, "ScontoGlobale"), out var sg)) d.ScontoGlobale = sg;
                    if (TryDec(rows.Get(row, "SpeseAccessorie"), out var sa)) d.SpeseAccessorie = sa;

                    ApplyIf(rows.Get(row, "ModalitaPagamento"), v => d.ModalitaPagamento = v);
                    ApplyIf(rows.Get(row, "BancaAppoggio"), v => d.BancaAppoggio = v);
                    if (TryDate(rows.Get(row, "DataScadenzaPagamento"), out var dsp)) d.DataScadenzaPagamento = dsp; else d.DataScadenzaPagamento = null;

                    if (TryBool(rows.Get(row, "Pagato"), out var pag)) d.Pagato = pag;
                    if (TryDate(rows.Get(row, "DataPagamento"), out var dp)) d.DataPagamento = dp; else d.DataPagamento = null;

                    ApplyIf(rows.Get(row, "PartitaIVADestinatario"), v => d.PartitaIVADestinatario = v);
                    ApplyIf(rows.Get(row, "CodiceFiscaleDestinatario"), v => d.CodiceFiscaleDestinatario = v);
                    ApplyIf(rows.Get(row, "CodiceSDI"), v => d.CodiceSDI = v);
                    ApplyIf(rows.Get(row, "PECDestinatario"), v => d.PECDestinatario = v);

                    if (TryBool(rows.Get(row, "FatturaElettronica"), out var fe)) d.FatturaElettronica = fe;
                    ApplyIf(rows.Get(row, "NomeFileXML"), v => d.NomeFileXML = v);
                    if (TryBool(rows.Get(row, "XMLInviato"), out var xi)) d.XMLInviato = xi;
                    if (TryDate(rows.Get(row, "DataInvioXML"), out var dix)) d.DataInvioXML = dix; else d.DataInvioXML = null;
                    ApplyIf(rows.Get(row, "IdentificativoSDI"), v => d.IdentificativoSDI = v);
                    ApplyIf(rows.Get(row, "StatoFatturaElettronica"), v => d.StatoFatturaElettronica = v);

                    var magCod = rows.Get(row, "MagazzinoCodice");
                    if (!string.IsNullOrWhiteSpace(magCod) && magazziniByCode.TryGetValue(magCod, out var mid)) d.MagazzinoId = mid;

                    ApplyIf(rows.Get(row, "CausaleDocumento"), v => d.CausaleDocumento = v);
                    ApplyIf(rows.Get(row, "AspettoBeni"), v => d.AspettoBeni = v);
                    ApplyIf(rows.Get(row, "TrasportoCura"), v => d.TrasportoCura = v);
                    ApplyIf(rows.Get(row, "Vettore"), v => d.Vettore = v);
                    if (TryInt(rows.Get(row, "NumeroColli"), out var nc)) d.NumeroColli = nc; else d.NumeroColli = null;
                    if (TryDec(rows.Get(row, "Peso"), out var peso)) d.Peso = peso; else d.Peso = null;
                    if (TryBool(rows.Get(row, "ReverseCharge"), out var rc)) d.ReverseCharge = rc;
                    if (TryBool(rows.Get(row, "SplitPayment"), out var sp)) d.SplitPayment = sp;
                    ApplyIf(rows.Get(row, "Note"), v => d.Note = v);
                    ApplyIf(rows.Get(row, "UtenteCreazione"), v => d.UtenteCreazione = v);

                    d.DataModifica = DateTime.Now;

                    // DocumentoOriginaleKey: risolviamo dopo aver importato tutti i documenti
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();

            // Second pass: DocumentoOriginaleId
            var byKey2 = ctx.Documenti.ToDictionary(d => DocKey(d.TipoDocumento, d.NumeroDocumento), d => d.Id, StringComparer.OrdinalIgnoreCase);
            ReadIfExists(Path.Combine(sourceDirectory, FileDocumenti), rows2 =>
            {
                foreach (var row in rows2.Records)
                {
                    try
                    {
                        var tipo = rows2.Get(row, "TipoDocumento");
                        var num = rows2.Get(row, "NumeroDocumento");
                        if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(num)) continue;
                        var key = DocKey(tipo, num);
                        if (!byKey2.TryGetValue(key, out var did)) continue;

                        var origKey = rows2.Get(row, "DocumentoOriginaleKey");
                        if (string.IsNullOrWhiteSpace(origKey) || !byKey2.TryGetValue(origKey, out var oid)) continue;

                        var doc = ctx.Documenti.Find(did);
                        if (doc != null)
                            doc.DocumentoOriginaleId = oid;
                    }
                    catch { /* best-effort */ }
                }
                ctx.SaveChanges();
            });
        }) };

        var documentiByKeyToId = ctx.Documenti.ToDictionary(d => DocKey(d.TipoDocumento, d.NumeroDocumento), d => d.Id, StringComparer.OrdinalIgnoreCase);

        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileDocumentiRighe), rows =>
        {
            var existing = ctx.DocumentiRighe.AsNoTracking().Select(r => new { r.DocumentoId, r.NumeroRiga, r.Descrizione, r.Quantita, r.PrezzoUnitario, r.Totale }).ToList();
            var set = new HashSet<string>(existing.Select(x => $"{x.DocumentoId}|{x.NumeroRiga}|{x.Descrizione}|{x.Quantita.ToString(CultureInfo.InvariantCulture)}|{x.PrezzoUnitario.ToString(CultureInfo.InvariantCulture)}|{x.Totale.ToString(CultureInfo.InvariantCulture)}"), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var docKey = rows.Get(row, "DocumentoKey");
                    if (string.IsNullOrWhiteSpace(docKey) || !documentiByKeyToId.TryGetValue(docKey, out var did)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!TryInt(rows.Get(row, "NumeroRiga"), out var nr)) nr = 0;

                    var descr = rows.Get(row, "Descrizione") ?? string.Empty;
                    var q = rows.Get(row, "Quantita") ?? string.Empty;
                    var pu = rows.Get(row, "PrezzoUnitario") ?? string.Empty;
                    var tot = rows.Get(row, "Totale") ?? string.Empty;

                    var key = $"{did}|{nr}|{descr}|{q}|{pu}|{tot}";
                    if (set.Contains(key)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    var r = new DocumentoRiga
                    {
                        DocumentoId = did,
                        NumeroRiga = nr,
                        Descrizione = descr,
                        Note = rows.Get(row, "Note")
                    };

                    var articoloCod = rows.Get(row, "ArticoloCodice");
                    if (!string.IsNullOrWhiteSpace(articoloCod) && articoliByCode.TryGetValue(articoloCod, out var aid)) r.ArticoloId = aid;

                    if (TryDec(rows.Get(row, "Quantita"), out var quant)) r.Quantita = quant;
                    ApplyIf(rows.Get(row, "UnitaMisura"), v => r.UnitaMisura = v);
                    if (TryDec(rows.Get(row, "PrezzoUnitario"), out var puv)) r.PrezzoUnitario = puv;
                    if (TryDec(rows.Get(row, "Sconto1"), out var s1)) r.Sconto1 = s1;
                    if (TryDec(rows.Get(row, "Sconto2"), out var s2)) r.Sconto2 = s2;
                    if (TryDec(rows.Get(row, "Sconto3"), out var s3)) r.Sconto3 = s3;
                    if (TryDec(rows.Get(row, "PrezzoNetto"), out var pn)) r.PrezzoNetto = pn;
                    if (TryDec(rows.Get(row, "AliquotaIVA"), out var iva)) r.AliquotaIVA = iva;
                    if (TryDec(rows.Get(row, "Imponibile"), out var imp)) r.Imponibile = imp;
                    if (TryDec(rows.Get(row, "ImportoIVA"), out var iiva)) r.ImportoIVA = iiva;
                    if (TryDec(rows.Get(row, "Totale"), out var tt)) r.Totale = tt;
                    ApplyIf(rows.Get(row, "NumeroSerie"), v => r.NumeroSerie = v);
                    ApplyIf(rows.Get(row, "Lotto"), v => r.Lotto = v);
                    if (TryBool(rows.Get(row, "RigaDescrittiva"), out var rd)) r.RigaDescrittiva = rd;

                    ctx.DocumentiRighe.Add(r);
                    set.Add(key);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Collegamenti documenti
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileDocumentoCollegamenti), rows =>
        {
            var existing = ctx.DocumentoCollegamenti.AsNoTracking().Select(x => new { x.DocumentoId, x.DocumentoOrigineId }).ToList();
            var set = new HashSet<string>(existing.Select(x => $"{x.DocumentoId}|{x.DocumentoOrigineId}"));

            foreach (var row in rows.Records)
            {
                try
                {
                    var dKey = rows.Get(row, "DocumentoKey");
                    var oKey = rows.Get(row, "DocumentoOrigineKey");
                    if (string.IsNullOrWhiteSpace(dKey) || string.IsNullOrWhiteSpace(oKey)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!documentiByKeyToId.TryGetValue(dKey, out var did) || !documentiByKeyToId.TryGetValue(oKey, out var oid)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    var k = $"{did}|{oid}";
                    if (set.Contains(k)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    ctx.DocumentoCollegamenti.Add(new DocumentoCollegamento { DocumentoId = did, DocumentoOrigineId = oid });
                    set.Add(k);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Movimenti magazzino: insert-only dedup best-effort
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileMovimentiMagazzino), rows =>
        {
            var existing = ctx.MovimentiMagazzino.AsNoTracking()
                .Select(m => new { m.TipoMovimento, m.DataMovimento, m.ArticoloId, m.MagazzinoId, m.Quantita, m.CostoUnitario, m.NumeroDocumento, m.NumeroSerie, m.Lotto })
                .ToList();

            var set = new HashSet<string>(existing.Select(x => $"{x.TipoMovimento}|{x.DataMovimento:O}|{x.ArticoloId}|{x.MagazzinoId}|{x.Quantita.ToString(CultureInfo.InvariantCulture)}|{x.CostoUnitario.ToString(CultureInfo.InvariantCulture)}|{x.NumeroDocumento}|{x.NumeroSerie}|{x.Lotto}"), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var tipo = rows.Get(row, "TipoMovimento");
                    var articoloCod = rows.Get(row, "ArticoloCodice");
                    var magCod = rows.Get(row, "MagazzinoCodice");
                    if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(articoloCod) || string.IsNullOrWhiteSpace(magCod)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!TryDate(rows.Get(row, "DataMovimento"), out var dm)) { res = res with { Errors = res.Errors + 1 }; continue; }
                    if (!articoliByCode.TryGetValue(articoloCod, out var aid) || !magazziniByCode.TryGetValue(magCod, out var mid)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    var quantRaw = rows.Get(row, "Quantita");
                    var costoRaw = rows.Get(row, "CostoUnitario");
                    if (!TryDec(quantRaw, out var q)) q = 0m;
                    if (!TryDec(costoRaw, out var cu)) cu = 0m;

                    var numDoc = rows.Get(row, "NumeroDocumento");
                    var ns = rows.Get(row, "NumeroSerie");
                    var lotto = rows.Get(row, "Lotto");

                    var key = $"{tipo}|{dm:O}|{aid}|{mid}|{q.ToString(CultureInfo.InvariantCulture)}|{cu.ToString(CultureInfo.InvariantCulture)}|{numDoc}|{ns}|{lotto}";
                    if (set.Contains(key)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    var m = new MovimentoMagazzino
                    {
                        TipoMovimento = tipo,
                        DataMovimento = dm,
                        ArticoloId = aid,
                        MagazzinoId = mid,
                        Quantita = q,
                        CostoUnitario = cu,
                        NumeroDocumento = numDoc,
                        NumeroSerie = ns,
                        Lotto = lotto,
                        Causale = rows.Get(row, "Causale"),
                        Note = rows.Get(row, "Note"),
                        UtenteCreazione = rows.Get(row, "UtenteCreazione")
                    };

                    var docKey = rows.Get(row, "DocumentoKey");
                    if (!string.IsNullOrWhiteSpace(docKey) && documentiByKeyToId.TryGetValue(docKey, out var did))
                        m.DocumentoId = did;

                    if (TryDate(rows.Get(row, "DataScadenza"), out var ds))
                        m.DataScadenza = ds;

                    ctx.MovimentiMagazzino.Add(m);
                    set.Add(key);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Documento archivio
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileDocumentiArchivio), rows =>
        {
            var byProt = ctx.DocumentiArchivio.ToDictionary(d => d.NumeroProtocollo.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var prot = rows.Get(row, "NumeroProtocollo");
                    if (string.IsNullOrWhiteSpace(prot)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byProt.TryGetValue(prot, out var d))
                    {
                        var titolo = rows.Get(row, "TitoloDocumento");
                        var percorso = rows.Get(row, "PercorsoFile");
                        if (string.IsNullOrWhiteSpace(titolo) || string.IsNullOrWhiteSpace(percorso)) { res = res with { Errors = res.Errors + 1 }; continue; }

                        d = new DocumentoArchivio { NumeroProtocollo = prot, TitoloDocumento = titolo, PercorsoFile = percorso };
                        ctx.DocumentiArchivio.Add(d);
                        byProt[prot] = d;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    if (TryDate(rows.Get(row, "DataProtocollo"), out var dp)) d.DataProtocollo = dp;
                    ApplyIf(rows.Get(row, "TitoloDocumento"), v => d.TitoloDocumento = v);
                    ApplyIf(rows.Get(row, "CategoriaDocumento"), v => d.CategoriaDocumento = v);
                    ApplyIf(rows.Get(row, "Descrizione"), v => d.Descrizione = v);
                    ApplyIf(rows.Get(row, "PercorsoFile"), v => d.PercorsoFile = v);
                    ApplyIf(rows.Get(row, "EstensioneFile"), v => d.EstensioneFile = v);
                    if (TryLong(rows.Get(row, "DimensioneFile"), out var dim)) d.DimensioneFile = dim;

                    var clienteCod = rows.Get(row, "ClienteCodice");
                    var fornitoreCod = rows.Get(row, "FornitoreCodice");
                    var articoloCod = rows.Get(row, "ArticoloCodice");
                    d.ClienteId = (!string.IsNullOrWhiteSpace(clienteCod) && clientiByCode.TryGetValue(clienteCod, out var cid)) ? cid : null;
                    d.FornitoreId = (!string.IsNullOrWhiteSpace(fornitoreCod) && fornitoriByCode.TryGetValue(fornitoreCod, out var fid)) ? fid : null;
                    d.ArticoloId = (!string.IsNullOrWhiteSpace(articoloCod) && articoliByCode.TryGetValue(articoloCod, out var aid)) ? aid : null;

                    ApplyIf(rows.Get(row, "StatoDocumento"), v => d.StatoDocumento = v);
                    if (TryDate(rows.Get(row, "DataApertura"), out var da)) d.DataApertura = da; else d.DataApertura = null;
                    if (TryDate(rows.Get(row, "DataChiusura"), out var dc)) d.DataChiusura = dc; else d.DataChiusura = null;
                    ApplyIf(rows.Get(row, "Tags"), v => d.Tags = v);
                    ApplyIf(rows.Get(row, "Note"), v => d.Note = v);
                    d.DataModifica = DateTime.Now;
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        // Contabilità
        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FilePianoDeiConti), rows =>
        {
            var byCod = ctx.PianiDeiConti.ToDictionary(c => c.Codice.Trim(), StringComparer.OrdinalIgnoreCase);

            // 1st pass create/update without parent
            foreach (var row in rows.Records)
            {
                try
                {
                    var cod = rows.Get(row, "Codice");
                    if (string.IsNullOrWhiteSpace(cod)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byCod.TryGetValue(cod, out var c))
                    {
                        var descr = rows.Get(row, "Descrizione");
                        if (string.IsNullOrWhiteSpace(descr)) { res = res with { Errors = res.Errors + 1 }; continue; }
                        c = new PianoDeiConti { Codice = cod, Descrizione = descr };
                        ctx.PianiDeiConti.Add(c);
                        byCod[cod] = c;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    ApplyIf(rows.Get(row, "Descrizione"), v => c.Descrizione = v);
                    ApplyIf(rows.Get(row, "TipoConto"), v => c.TipoConto = v);
                    if (TryInt(rows.Get(row, "Livello"), out var liv)) c.Livello = liv;
                    if (TryBool(rows.Get(row, "ContoFoglia"), out var fog)) c.ContoFoglia = fog;
                    if (TryBool(rows.Get(row, "Attivo"), out var att)) c.Attivo = att;
                    ApplyIf(rows.Get(row, "Note"), v => c.Note = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }
            ctx.SaveChanges();

            var idByCod = ctx.PianiDeiConti.ToDictionary(c => c.Codice.Trim(), c => c.Id, StringComparer.OrdinalIgnoreCase);

            // 2nd pass set parent
            foreach (var row in rows.Records)
            {
                try
                {
                    var cod = rows.Get(row, "Codice");
                    var sup = rows.Get(row, "ContoSuperioreCodice");
                    if (string.IsNullOrWhiteSpace(cod) || string.IsNullOrWhiteSpace(sup)) continue;
                    if (!idByCod.TryGetValue(cod, out var id) || !idByCod.TryGetValue(sup, out var sid)) continue;
                    var c = ctx.PianiDeiConti.Find(id);
                    if (c != null)
                        c.ContoSuperioreId = sid;
                }
                catch { /* best-effort */ }
            }
            ctx.SaveChanges();
        }) };

        var contiByCodToId = ctx.PianiDeiConti.ToDictionary(c => c.Codice.Trim(), c => c.Id, StringComparer.OrdinalIgnoreCase);

        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileRegistrazioni), rows =>
        {
            var byNum = ctx.RegistrazioniContabili.ToDictionary(r => r.NumeroRegistrazione.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var num = rows.Get(row, "NumeroRegistrazione");
                    if (string.IsNullOrWhiteSpace(num)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    if (!byNum.TryGetValue(num, out var r))
                    {
                        r = new RegistrazioneContabile { NumeroRegistrazione = num };
                        ctx.RegistrazioniContabili.Add(r);
                        byNum[num] = r;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        res = res with { Updated = res.Updated + 1 };
                    }

                    if (TryDate(rows.Get(row, "DataRegistrazione"), out var dr)) r.DataRegistrazione = dr;
                    ApplyIf(rows.Get(row, "CausaleContabile"), v => r.CausaleContabile = v);
                    ApplyIf(rows.Get(row, "Descrizione"), v => r.Descrizione = v);
                    var docKey = rows.Get(row, "DocumentoKey");
                    if (!string.IsNullOrWhiteSpace(docKey) && documentiByKeyToId.TryGetValue(docKey, out var did)) r.DocumentoId = did;
                    if (TryDec(rows.Get(row, "TotaleDare"), out var td)) r.TotaleDare = td;
                    if (TryDec(rows.Get(row, "TotaleAvere"), out var ta)) r.TotaleAvere = ta;
                    ApplyIf(rows.Get(row, "UtenteCreazione"), v => r.UtenteCreazione = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        var regByNumToId = ctx.RegistrazioniContabili.ToDictionary(r => r.NumeroRegistrazione.Trim(), r => r.Id, StringComparer.OrdinalIgnoreCase);

        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileMovimentiContabili), rows =>
        {
            var existing = ctx.MovimentiContabili.AsNoTracking().Select(m => new { m.RegistrazioneId, m.ContoId, m.ImportoDare, m.ImportoAvere, m.Descrizione }).ToList();
            var set = new HashSet<string>(existing.Select(x => $"{x.RegistrazioneId}|{x.ContoId}|{x.ImportoDare.ToString(CultureInfo.InvariantCulture)}|{x.ImportoAvere.ToString(CultureInfo.InvariantCulture)}|{x.Descrizione}"), StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var numReg = rows.Get(row, "NumeroRegistrazione");
                    var contoCod = rows.Get(row, "ContoCodice");
                    if (string.IsNullOrWhiteSpace(numReg) || string.IsNullOrWhiteSpace(contoCod)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!regByNumToId.TryGetValue(numReg, out var rid) || !contiByCodToId.TryGetValue(contoCod, out var cid)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    var dareRaw = rows.Get(row, "ImportoDare");
                    var avereRaw = rows.Get(row, "ImportoAvere");
                    if (!TryDec(dareRaw, out var dare)) dare = 0m;
                    if (!TryDec(avereRaw, out var avere)) avere = 0m;
                    var descr = rows.Get(row, "Descrizione");

                    var key = $"{rid}|{cid}|{dare.ToString(CultureInfo.InvariantCulture)}|{avere.ToString(CultureInfo.InvariantCulture)}|{descr}";
                    if (set.Contains(key)) { res = res with { Skipped = res.Skipped + 1 }; continue; }

                    ctx.MovimentiContabili.Add(new MovimentoContabile
                    {
                        RegistrazioneId = rid,
                        ContoId = cid,
                        ImportoDare = dare,
                        ImportoAvere = avere,
                        Descrizione = descr
                    });

                    set.Add(key);
                    res = res with { Inserted = res.Inserted + 1 };
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        res = res with { FilesRead = res.FilesRead + ReadIfExists(Path.Combine(sourceDirectory, FileRegistriIva), rows =>
        {
            var byKey = ctx.RegistriIVA.ToDictionary(r => $"{r.TipoRegistro}|{r.NumeroProtocollo}", StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.Records)
            {
                try
                {
                    var tipo = rows.Get(row, "TipoRegistro");
                    var prot = rows.Get(row, "NumeroProtocollo");
                    var docKey = rows.Get(row, "DocumentoKey");
                    if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(prot) || string.IsNullOrWhiteSpace(docKey)) { res = res with { Skipped = res.Skipped + 1 }; continue; }
                    if (!documentiByKeyToId.TryGetValue(docKey, out var did)) { res = res with { Errors = res.Errors + 1 }; continue; }

                    var key = $"{tipo}|{prot}";
                    if (!byKey.TryGetValue(key, out var r))
                    {
                        r = new RegistroIVA { TipoRegistro = tipo, NumeroProtocollo = prot, DocumentoId = did, DataRegistrazione = DateTime.Today };
                        ctx.RegistriIVA.Add(r);
                        byKey[key] = r;
                        res = res with { Inserted = res.Inserted + 1 };
                    }
                    else
                    {
                        r.DocumentoId = did;
                        res = res with { Updated = res.Updated + 1 };
                    }

                    if (TryDate(rows.Get(row, "DataRegistrazione"), out var dr)) r.DataRegistrazione = dr;
                    if (TryDec(rows.Get(row, "Imponibile"), out var imp)) r.Imponibile = imp;
                    if (TryDec(rows.Get(row, "AliquotaIVA"), out var iva)) r.AliquotaIVA = iva;
                    if (TryDec(rows.Get(row, "ImportoIVA"), out var iiva)) r.ImportoIVA = iiva;
                    if (TryDec(rows.Get(row, "IVADetraibile"), out var det)) r.IVADetraibile = det;
                    if (TryDec(rows.Get(row, "IVAIndetraibile"), out var ind)) r.IVAIndetraibile = ind;
                    if (TryBool(rows.Get(row, "EsigibilitaDifferita"), out var ed)) r.EsigibilitaDifferita = ed;
                    if (TryDate(rows.Get(row, "DataEsigibilita"), out var de)) r.DataEsigibilita = de; else r.DataEsigibilita = null;
                    ApplyIf(rows.Get(row, "Descrizione"), v => r.Descrizione = v);
                }
                catch
                {
                    res = res with { Errors = res.Errors + 1 };
                }
            }

            ctx.SaveChanges();
        }) };

        return res;
    }

    // ===== CSV low-level =====

    private sealed class CsvRows
    {
        public CsvRows(List<string> header, List<List<string>> records)
        {
            HeaderRow = header;
            Records = records;
            Header = BuildHeaderMap(header);
        }

        public List<string> HeaderRow { get; }
        public List<List<string>> Records { get; }
        public Dictionary<string, int> Header { get; }

        public string? Get(IReadOnlyList<string> row, params string[] names)
        {
            foreach (var n in names)
            {
                var key = NormalizeHeader(n);
                if (!Header.TryGetValue(key, out var idx))
                    continue;
                if (idx < 0 || idx >= row.Count)
                    continue;
                var v = row[idx];
                if (v == null)
                    return null;
                var t = v.Trim();
                return t.Length == 0 ? null : t;
            }

            return null;
        }
    }

    private static int ReadIfExists(string filePath, Action<CsvRows> handle)
    {
        if (!File.Exists(filePath))
            return 0;

        using var sr = new StreamReader(File.OpenRead(filePath), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var all = ReadRecords(sr, Sep).ToList();
        if (all.Count == 0)
            return 1;

        var header = all[0];
        var records = all.Skip(1).ToList();
        handle(new CsvRows(header, records));
        return 1;
    }

    private static void WriteCsv(string filePath, IEnumerable<string> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        AppendRow(sb, header, Sep);
        foreach (var r in rows)
            AppendRow(sb, r, Sep);

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void AppendRow(StringBuilder sb, IEnumerable<string?> values, char separator)
    {
        var first = true;
        foreach (var v in values)
        {
            if (!first)
                sb.Append(separator);
            first = false;
            sb.Append(ToCsvField(v ?? string.Empty, separator));
        }
        sb.AppendLine();
    }

    private static string ToCsvField(string value, char separator)
    {
        var needsQuotes = value.Contains('"') || value.Contains('\n') || value.Contains('\r') || value.Contains(separator);
        if (!needsQuotes)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static IEnumerable<List<string>> ReadRecords(TextReader reader, char separator)
    {
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        int ch;
        while ((ch = reader.Read()) != -1)
        {
            var c = (char)ch;

            if (inQuotes)
            {
                if (c == '"')
                {
                    var next = reader.Peek();
                    if (next == '"')
                    {
                        reader.Read();
                        field.Append('"');
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                field.Append(c);
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                continue;
            }

            if (c == separator)
            {
                record.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (c == '\r')
            {
                if (reader.Peek() == '\n')
                    reader.Read();

                record.Add(field.ToString());
                field.Clear();

                if (record.Count > 1 || record[0].Length > 0)
                    yield return record;

                record = new List<string>();
                continue;
            }

            if (c == '\n')
            {
                record.Add(field.ToString());
                field.Clear();

                if (record.Count > 1 || record[0].Length > 0)
                    yield return record;

                record = new List<string>();
                continue;
            }

            field.Append(c);
        }

        record.Add(field.ToString());
        if (record.Count > 1 || record[0].Length > 0)
            yield return record;
    }

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            var key = NormalizeHeader(header[i]);
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!map.ContainsKey(key))
                map[key] = i;
        }
        return map;
    }

    private static string NormalizeHeader(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        s = s.Replace("_", string.Empty);
        s = s.Replace(" ", string.Empty);
        s = s.Replace("-", string.Empty);
        s = s.Replace("à", "a", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("è", "e", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("é", "e", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("ì", "i", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("ò", "o", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("ù", "u", StringComparison.OrdinalIgnoreCase);
        return s;
    }

    // ===== Small helpers =====

    private static string DocKey(string tipo, string numero) => (tipo ?? string.Empty).Trim() + "|" + (numero ?? string.Empty).Trim();
    private static string OrdKey(string tipo, string numero) => (tipo ?? string.Empty).Trim() + "|" + (numero ?? string.Empty).Trim();
    private static string PriceKey(int listinoId, int articoloId, DateTime dataInizio) => $"{listinoId}|{articoloId}|{dataInizio:yyyy-MM-dd}";

    private static string FmtDate(DateTime dt) => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string FmtDec(decimal v) => v.ToString(CultureInfo.InvariantCulture);

    private static void ApplyIf(string? value, Action<string> apply)
    {
        if (!string.IsNullOrWhiteSpace(value))
            apply(value.Trim());
    }

    private static bool TryDec(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out value))
            return true;
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryInt(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               || int.TryParse(s, NumberStyles.Integer, CultureInfo.GetCultureInfo("it-IT"), out value);
    }

    private static bool TryLong(string? raw, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               || long.TryParse(s, NumberStyles.Integer, CultureInfo.GetCultureInfo("it-IT"), out value);
    }

    private static bool TryBool(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        if (bool.TryParse(s, out value))
            return true;

        if (string.Equals(s, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "S", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "SI", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (string.Equals(s, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "N", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "NO", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        return false;
    }

    private static bool TryDate(string? raw, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value)
               || DateTime.TryParse(s, CultureInfo.GetCultureInfo("it-IT"), DateTimeStyles.AssumeLocal, out value);
    }
}
