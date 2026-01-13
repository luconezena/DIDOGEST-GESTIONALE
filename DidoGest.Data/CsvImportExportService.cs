using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DidoGest.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data;

public sealed record CsvImportResult(int Inserted, int Updated, int Skipped, int Errors)
{
    public int Total => Inserted + Updated + Skipped + Errors;
}

public static class CsvImportExportService
{
    private const char DefaultSeparator = ';';

    // ===== Public API (UI-friendly) =====

    public static int ExportClienti(string filePath, char separator = DefaultSeparator)
    {
        using var ctx = DidoGestDb.CreateContext();

        var clienti = ctx.Clienti
            .AsNoTracking()
            .OrderBy(c => c.Codice)
            .ToList();

        var headers = new[]
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
            "Note"
        };

        var sb = new StringBuilder();
        AppendRow(sb, headers, separator);

        foreach (var c in clienti)
        {
            AppendRow(sb, new[]
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
                FormatDecimal(c.FidoMassimo),
                c.GiorniPagamento?.ToString(CultureInfo.InvariantCulture),
                c.Banca,
                c.IBAN,
                c.Attivo ? "1" : "0",
                c.Note
            }, separator);
        }

        WriteUtf8Bom(filePath, sb.ToString());
        return clienti.Count;
    }

    public static CsvImportResult ImportClienti(string filePath, char separator = DefaultSeparator)
    {
        using var ctx = DidoGestDb.CreateContext();
        var existingByCode = ctx.Clienti.ToDictionary(c => (c.Codice ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

        var result = new CsvImportResult(0, 0, 0, 0);
        var pending = 0;

        using var sr = OpenText(filePath);
        using var enumerator = ReadRecords(sr, separator).GetEnumerator();
        if (!enumerator.MoveNext())
            return result;

        var header = enumerator.Current;
        var map = BuildHeaderMap(header);

        while (enumerator.MoveNext())
        {
            var row = enumerator.Current;
            try
            {
                var codice = Get(row, map, "Codice", "CodiceCliente", "Cod");
                if (string.IsNullOrWhiteSpace(codice))
                {
                    result = result with { Skipped = result.Skipped + 1 };
                    continue;
                }

                var ragSoc = Get(row, map, "RagioneSociale", "Ragione", "RagSoc", "Denominazione");

                if (existingByCode.TryGetValue(codice.Trim(), out var cliente))
                {
                    ApplyIfNonEmpty(() => cliente.RagioneSociale = ragSoc!, ragSoc);
                    ApplyIfNonEmpty(() => cliente.Nome = Get(row, map, "Nome"), Get(row, map, "Nome"));
                    ApplyIfNonEmpty(() => cliente.Cognome = Get(row, map, "Cognome"), Get(row, map, "Cognome"));
                    ApplyIfNonEmpty(() => cliente.CodiceFiscale = Get(row, map, "CodiceFiscale", "CF"), Get(row, map, "CodiceFiscale", "CF"));
                    ApplyIfNonEmpty(() => cliente.PartitaIVA = Get(row, map, "PartitaIVA", "PIVA", "VAT"), Get(row, map, "PartitaIVA", "PIVA", "VAT"));
                    ApplyIfNonEmpty(() => cliente.Indirizzo = Get(row, map, "Indirizzo"), Get(row, map, "Indirizzo"));
                    ApplyIfNonEmpty(() => cliente.CAP = Get(row, map, "CAP"), Get(row, map, "CAP"));
                    ApplyIfNonEmpty(() => cliente.Citta = Get(row, map, "Citta", "Città"), Get(row, map, "Citta", "Città"));
                    ApplyIfNonEmpty(() => cliente.Provincia = Get(row, map, "Provincia", "Prov"), Get(row, map, "Provincia", "Prov"));
                    ApplyIfNonEmpty(() => cliente.Nazione = Get(row, map, "Nazione"), Get(row, map, "Nazione"));
                    ApplyIfNonEmpty(() => cliente.Telefono = Get(row, map, "Telefono", "Tel"), Get(row, map, "Telefono", "Tel"));
                    ApplyIfNonEmpty(() => cliente.Cellulare = Get(row, map, "Cellulare", "Cell"), Get(row, map, "Cellulare", "Cell"));
                    ApplyIfNonEmpty(() => cliente.Email = Get(row, map, "Email"), Get(row, map, "Email"));
                    ApplyIfNonEmpty(() => cliente.PEC = Get(row, map, "PEC"), Get(row, map, "PEC"));
                    ApplyIfNonEmpty(() => cliente.CodiceSDI = Get(row, map, "CodiceSDI", "SDI"), Get(row, map, "CodiceSDI", "SDI"));

                    var fidoRaw = Get(row, map, "FidoMassimo", "Fido");
                    if (TryParseDecimal(fidoRaw, out var fido))
                        cliente.FidoMassimo = fido;

                    var gpRaw = Get(row, map, "GiorniPagamento", "Giorni");
                    if (TryParseInt(gpRaw, out var gp))
                        cliente.GiorniPagamento = gp;

                    ApplyIfNonEmpty(() => cliente.Banca = Get(row, map, "Banca"), Get(row, map, "Banca"));
                    ApplyIfNonEmpty(() => cliente.IBAN = Get(row, map, "IBAN"), Get(row, map, "IBAN"));

                    var attivoRaw = Get(row, map, "Attivo");
                    if (TryParseBool(attivoRaw, out var attivo))
                        cliente.Attivo = attivo;

                    ApplyIfNonEmpty(() => cliente.Note = Get(row, map, "Note"), Get(row, map, "Note"));

                    cliente.DataModifica = DateTime.Now;
                    result = result with { Updated = result.Updated + 1 };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(ragSoc))
                    {
                        result = result with { Errors = result.Errors + 1 };
                        continue;
                    }

                    var clienteNew = new Cliente
                    {
                        Codice = codice.Trim(),
                        RagioneSociale = ragSoc.Trim(),
                        Nome = NullIfBlank(Get(row, map, "Nome")),
                        Cognome = NullIfBlank(Get(row, map, "Cognome")),
                        CodiceFiscale = NullIfBlank(Get(row, map, "CodiceFiscale", "CF")),
                        PartitaIVA = NullIfBlank(Get(row, map, "PartitaIVA", "PIVA", "VAT")),
                        Indirizzo = NullIfBlank(Get(row, map, "Indirizzo")),
                        CAP = NullIfBlank(Get(row, map, "CAP")),
                        Citta = NullIfBlank(Get(row, map, "Citta", "Città")),
                        Provincia = NullIfBlank(Get(row, map, "Provincia", "Prov")),
                        Nazione = NullIfBlank(Get(row, map, "Nazione")),
                        Telefono = NullIfBlank(Get(row, map, "Telefono", "Tel")),
                        Cellulare = NullIfBlank(Get(row, map, "Cellulare", "Cell")),
                        Email = NullIfBlank(Get(row, map, "Email")),
                        PEC = NullIfBlank(Get(row, map, "PEC")),
                        CodiceSDI = NullIfBlank(Get(row, map, "CodiceSDI", "SDI")),
                        Banca = NullIfBlank(Get(row, map, "Banca")),
                        IBAN = NullIfBlank(Get(row, map, "IBAN")),
                        Note = NullIfBlank(Get(row, map, "Note")),
                        Attivo = true,
                        DataCreazione = DateTime.Now
                    };

                    var fidoRaw = Get(row, map, "FidoMassimo", "Fido");
                    if (TryParseDecimal(fidoRaw, out var fido))
                        clienteNew.FidoMassimo = fido;

                    var gpRaw = Get(row, map, "GiorniPagamento", "Giorni");
                    if (TryParseInt(gpRaw, out var gp))
                        clienteNew.GiorniPagamento = gp;

                    var attivoRaw = Get(row, map, "Attivo");
                    if (TryParseBool(attivoRaw, out var attivo))
                        clienteNew.Attivo = attivo;

                    ctx.Clienti.Add(clienteNew);
                    existingByCode[codice.Trim()] = clienteNew;
                    result = result with { Inserted = result.Inserted + 1 };
                }

                pending++;
                if (pending >= 200)
                {
                    ctx.SaveChanges();
                    pending = 0;
                }
            }
            catch
            {
                result = result with { Errors = result.Errors + 1 };
            }
        }

        if (pending > 0)
            ctx.SaveChanges();

        return result;
    }

    public static int ExportFornitori(string filePath, char separator = DefaultSeparator)
    {
        using var ctx = DidoGestDb.CreateContext();

        var fornitori = ctx.Fornitori
            .AsNoTracking()
            .OrderBy(f => f.Codice)
            .ToList();

        var headers = new[]
        {
            "Codice",
            "RagioneSociale",
            "CodiceFiscale",
            "PartitaIVA",
            "Indirizzo",
            "CAP",
            "Citta",
            "Provincia",
            "Nazione",
            "Telefono",
            "Email",
            "PEC",
            "CodiceSDI",
            "GiorniPagamento",
            "Banca",
            "IBAN",
            "ValutazioneQualita",
            "DataUltimaValutazione",
            "Attivo",
            "Note"
        };

        var sb = new StringBuilder();
        AppendRow(sb, headers, separator);

        foreach (var f in fornitori)
        {
            AppendRow(sb, new[]
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
                FormatDecimal(f.ValutazioneQualita),
                f.DataUltimaValutazione?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                f.Attivo ? "1" : "0",
                f.Note
            }, separator);
        }

        WriteUtf8Bom(filePath, sb.ToString());
        return fornitori.Count;
    }

    public static CsvImportResult ImportFornitori(string filePath, char separator = DefaultSeparator)
    {
        using var ctx = DidoGestDb.CreateContext();
        var existingByCode = ctx.Fornitori.ToDictionary(f => (f.Codice ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

        var result = new CsvImportResult(0, 0, 0, 0);
        var pending = 0;

        using var sr = OpenText(filePath);
        using var enumerator = ReadRecords(sr, separator).GetEnumerator();
        if (!enumerator.MoveNext())
            return result;

        var header = enumerator.Current;
        var map = BuildHeaderMap(header);

        while (enumerator.MoveNext())
        {
            var row = enumerator.Current;
            try
            {
                var codice = Get(row, map, "Codice", "CodiceFornitore", "Cod");
                if (string.IsNullOrWhiteSpace(codice))
                {
                    result = result with { Skipped = result.Skipped + 1 };
                    continue;
                }

                var ragSoc = Get(row, map, "RagioneSociale", "Ragione", "RagSoc", "Denominazione");

                if (existingByCode.TryGetValue(codice.Trim(), out var f))
                {
                    ApplyIfNonEmpty(() => f.RagioneSociale = ragSoc!, ragSoc);
                    ApplyIfNonEmpty(() => f.CodiceFiscale = Get(row, map, "CodiceFiscale", "CF"), Get(row, map, "CodiceFiscale", "CF"));
                    ApplyIfNonEmpty(() => f.PartitaIVA = Get(row, map, "PartitaIVA", "PIVA", "VAT"), Get(row, map, "PartitaIVA", "PIVA", "VAT"));
                    ApplyIfNonEmpty(() => f.Indirizzo = Get(row, map, "Indirizzo"), Get(row, map, "Indirizzo"));
                    ApplyIfNonEmpty(() => f.CAP = Get(row, map, "CAP"), Get(row, map, "CAP"));
                    ApplyIfNonEmpty(() => f.Citta = Get(row, map, "Citta", "Città"), Get(row, map, "Citta", "Città"));
                    ApplyIfNonEmpty(() => f.Provincia = Get(row, map, "Provincia", "Prov"), Get(row, map, "Provincia", "Prov"));
                    ApplyIfNonEmpty(() => f.Nazione = Get(row, map, "Nazione"), Get(row, map, "Nazione"));
                    ApplyIfNonEmpty(() => f.Telefono = Get(row, map, "Telefono", "Tel"), Get(row, map, "Telefono", "Tel"));
                    ApplyIfNonEmpty(() => f.Email = Get(row, map, "Email"), Get(row, map, "Email"));
                    ApplyIfNonEmpty(() => f.PEC = Get(row, map, "PEC"), Get(row, map, "PEC"));
                    ApplyIfNonEmpty(() => f.CodiceSDI = Get(row, map, "CodiceSDI", "SDI"), Get(row, map, "CodiceSDI", "SDI"));

                    var gpRaw = Get(row, map, "GiorniPagamento", "Giorni");
                    if (TryParseInt(gpRaw, out var gp))
                        f.GiorniPagamento = gp;

                    ApplyIfNonEmpty(() => f.Banca = Get(row, map, "Banca"), Get(row, map, "Banca"));
                    ApplyIfNonEmpty(() => f.IBAN = Get(row, map, "IBAN"), Get(row, map, "IBAN"));

                    var valRaw = Get(row, map, "ValutazioneQualita", "Valutazione");
                    if (TryParseDecimal(valRaw, out var val))
                        f.ValutazioneQualita = val;

                    var dataValRaw = Get(row, map, "DataUltimaValutazione", "UltimaValutazione");
                    if (TryParseDate(dataValRaw, out var dt))
                        f.DataUltimaValutazione = dt;

                    var attivoRaw = Get(row, map, "Attivo");
                    if (TryParseBool(attivoRaw, out var attivo))
                        f.Attivo = attivo;

                    ApplyIfNonEmpty(() => f.Note = Get(row, map, "Note"), Get(row, map, "Note"));

                    f.DataModifica = DateTime.Now;
                    result = result with { Updated = result.Updated + 1 };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(ragSoc))
                    {
                        result = result with { Errors = result.Errors + 1 };
                        continue;
                    }

                    var fNew = new Fornitore
                    {
                        Codice = codice.Trim(),
                        RagioneSociale = ragSoc.Trim(),
                        CodiceFiscale = NullIfBlank(Get(row, map, "CodiceFiscale", "CF")),
                        PartitaIVA = NullIfBlank(Get(row, map, "PartitaIVA", "PIVA", "VAT")),
                        Indirizzo = NullIfBlank(Get(row, map, "Indirizzo")),
                        CAP = NullIfBlank(Get(row, map, "CAP")),
                        Citta = NullIfBlank(Get(row, map, "Citta", "Città")),
                        Provincia = NullIfBlank(Get(row, map, "Provincia", "Prov")),
                        Nazione = NullIfBlank(Get(row, map, "Nazione")),
                        Telefono = NullIfBlank(Get(row, map, "Telefono", "Tel")),
                        Email = NullIfBlank(Get(row, map, "Email")),
                        PEC = NullIfBlank(Get(row, map, "PEC")),
                        CodiceSDI = NullIfBlank(Get(row, map, "CodiceSDI", "SDI")),
                        Banca = NullIfBlank(Get(row, map, "Banca")),
                        IBAN = NullIfBlank(Get(row, map, "IBAN")),
                        Note = NullIfBlank(Get(row, map, "Note")),
                        Attivo = true,
                        DataCreazione = DateTime.Now
                    };

                    var gpRaw = Get(row, map, "GiorniPagamento", "Giorni");
                    if (TryParseInt(gpRaw, out var gp))
                        fNew.GiorniPagamento = gp;

                    var valRaw = Get(row, map, "ValutazioneQualita", "Valutazione");
                    if (TryParseDecimal(valRaw, out var val))
                        fNew.ValutazioneQualita = val;

                    var dataValRaw = Get(row, map, "DataUltimaValutazione", "UltimaValutazione");
                    if (TryParseDate(dataValRaw, out var dt))
                        fNew.DataUltimaValutazione = dt;

                    var attivoRaw = Get(row, map, "Attivo");
                    if (TryParseBool(attivoRaw, out var attivo))
                        fNew.Attivo = attivo;

                    ctx.Fornitori.Add(fNew);
                    existingByCode[codice.Trim()] = fNew;
                    result = result with { Inserted = result.Inserted + 1 };
                }

                pending++;
                if (pending >= 200)
                {
                    ctx.SaveChanges();
                    pending = 0;
                }
            }
            catch
            {
                result = result with { Errors = result.Errors + 1 };
            }
        }

        if (pending > 0)
            ctx.SaveChanges();

        return result;
    }

    public static int ExportArticoli(string filePath, char separator = DefaultSeparator)
    {
        using var ctx = DidoGestDb.CreateContext();

        var articoli = ctx.Articoli
            .AsNoTracking()
            .OrderBy(a => a.Codice)
            .ToList();

        var headers = new[]
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
            "Categoria",
            "Sottocategoria",
            "Marca",
            "ArticoloDiServizio",
            "Attivo",
            "Note"
        };

        var sb = new StringBuilder();
        AppendRow(sb, headers, separator);

        foreach (var a in articoli)
        {
            AppendRow(sb, new[]
            {
                a.Codice,
                a.Descrizione,
                a.DescrizioneEstesa,
                a.CodiceEAN,
                a.CodiceFornitori,
                a.UnitaMisura,
                FormatDecimal(a.PrezzoAcquisto),
                FormatDecimal(a.PrezzoVendita),
                FormatDecimal(a.AliquotaIVA),
                FormatDecimal(a.ScortaMinima),
                a.Categoria,
                a.Sottocategoria,
                a.Marca,
                a.ArticoloDiServizio ? "1" : "0",
                a.Attivo ? "1" : "0",
                a.Note
            }, separator);
        }

        WriteUtf8Bom(filePath, sb.ToString());
        return articoli.Count;
    }

    public static CsvImportResult ImportArticoli(string filePath, char separator = DefaultSeparator)
    {
        using var ctx = DidoGestDb.CreateContext();
        var existingByCode = ctx.Articoli.ToDictionary(a => (a.Codice ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

        var result = new CsvImportResult(0, 0, 0, 0);
        var pending = 0;

        using var sr = OpenText(filePath);
        using var enumerator = ReadRecords(sr, separator).GetEnumerator();
        if (!enumerator.MoveNext())
            return result;

        var header = enumerator.Current;
        var map = BuildHeaderMap(header);

        while (enumerator.MoveNext())
        {
            var row = enumerator.Current;
            try
            {
                var codice = Get(row, map, "Codice", "CodiceArticolo", "SKU", "Cod");
                if (string.IsNullOrWhiteSpace(codice))
                {
                    result = result with { Skipped = result.Skipped + 1 };
                    continue;
                }

                var descr = Get(row, map, "Descrizione", "Desc", "Nome");

                if (existingByCode.TryGetValue(codice.Trim(), out var a))
                {
                    ApplyIfNonEmpty(() => a.Descrizione = descr!, descr);
                    ApplyIfNonEmpty(() => a.DescrizioneEstesa = Get(row, map, "DescrizioneEstesa"), Get(row, map, "DescrizioneEstesa"));
                    ApplyIfNonEmpty(() => a.CodiceEAN = Get(row, map, "CodiceEAN", "EAN"), Get(row, map, "CodiceEAN", "EAN"));
                    ApplyIfNonEmpty(() => a.CodiceFornitori = Get(row, map, "CodiceFornitori"), Get(row, map, "CodiceFornitori"));
                    ApplyIfNonEmpty(() => a.UnitaMisura = Get(row, map, "UnitaMisura", "UM"), Get(row, map, "UnitaMisura", "UM"));

                    var pAcqRaw = Get(row, map, "PrezzoAcquisto", "Costo");
                    if (TryParseDecimal(pAcqRaw, out var pAcq))
                        a.PrezzoAcquisto = pAcq;

                    var pVenRaw = Get(row, map, "PrezzoVendita", "Prezzo");
                    if (TryParseDecimal(pVenRaw, out var pVen))
                        a.PrezzoVendita = pVen;

                    var ivaRaw = Get(row, map, "AliquotaIVA", "IVA");
                    if (TryParseDecimal(ivaRaw, out var iva))
                        a.AliquotaIVA = iva;

                    var scMinRaw = Get(row, map, "ScortaMinima", "Scorta");
                    if (TryParseDecimal(scMinRaw, out var scMin))
                        a.ScortaMinima = scMin;

                    ApplyIfNonEmpty(() => a.Categoria = Get(row, map, "Categoria"), Get(row, map, "Categoria"));
                    ApplyIfNonEmpty(() => a.Sottocategoria = Get(row, map, "Sottocategoria"), Get(row, map, "Sottocategoria"));
                    ApplyIfNonEmpty(() => a.Marca = Get(row, map, "Marca"), Get(row, map, "Marca"));

                    var servRaw = Get(row, map, "ArticoloDiServizio", "Servizio");
                    if (TryParseBool(servRaw, out var serv))
                        a.ArticoloDiServizio = serv;

                    var attivoRaw = Get(row, map, "Attivo");
                    if (TryParseBool(attivoRaw, out var attivo))
                        a.Attivo = attivo;

                    ApplyIfNonEmpty(() => a.Note = Get(row, map, "Note"), Get(row, map, "Note"));

                    a.DataModifica = DateTime.Now;
                    result = result with { Updated = result.Updated + 1 };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(descr))
                    {
                        result = result with { Errors = result.Errors + 1 };
                        continue;
                    }

                    var aNew = new Articolo
                    {
                        Codice = codice.Trim(),
                        Descrizione = descr.Trim(),
                        DescrizioneEstesa = NullIfBlank(Get(row, map, "DescrizioneEstesa")),
                        CodiceEAN = NullIfBlank(Get(row, map, "CodiceEAN", "EAN")),
                        CodiceFornitori = NullIfBlank(Get(row, map, "CodiceFornitori")),
                        UnitaMisura = NullIfBlank(Get(row, map, "UnitaMisura", "UM")),
                        Categoria = NullIfBlank(Get(row, map, "Categoria")),
                        Sottocategoria = NullIfBlank(Get(row, map, "Sottocategoria")),
                        Marca = NullIfBlank(Get(row, map, "Marca")),
                        Note = NullIfBlank(Get(row, map, "Note")),
                        Attivo = true,
                        DataCreazione = DateTime.Now
                    };

                    var pAcqRaw = Get(row, map, "PrezzoAcquisto", "Costo");
                    if (TryParseDecimal(pAcqRaw, out var pAcq))
                        aNew.PrezzoAcquisto = pAcq;

                    var pVenRaw = Get(row, map, "PrezzoVendita", "Prezzo");
                    if (TryParseDecimal(pVenRaw, out var pVen))
                        aNew.PrezzoVendita = pVen;

                    var ivaRaw = Get(row, map, "AliquotaIVA", "IVA");
                    if (TryParseDecimal(ivaRaw, out var iva))
                        aNew.AliquotaIVA = iva;

                    var scMinRaw = Get(row, map, "ScortaMinima", "Scorta");
                    if (TryParseDecimal(scMinRaw, out var scMin))
                        aNew.ScortaMinima = scMin;

                    var servRaw = Get(row, map, "ArticoloDiServizio", "Servizio");
                    if (TryParseBool(servRaw, out var serv))
                        aNew.ArticoloDiServizio = serv;

                    var attivoRaw = Get(row, map, "Attivo");
                    if (TryParseBool(attivoRaw, out var attivo))
                        aNew.Attivo = attivo;

                    ctx.Articoli.Add(aNew);
                    existingByCode[codice.Trim()] = aNew;
                    result = result with { Inserted = result.Inserted + 1 };
                }

                pending++;
                if (pending >= 200)
                {
                    ctx.SaveChanges();
                    pending = 0;
                }
            }
            catch
            {
                result = result with { Errors = result.Errors + 1 };
            }
        }

        if (pending > 0)
            ctx.SaveChanges();

        return result;
    }

    // ===== CSV helpers =====

    private static void WriteUtf8Bom(string filePath, string content)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static StreamReader OpenText(string filePath)
    {
        // detect BOM when present
        return new StreamReader(File.OpenRead(filePath), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
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
        // Minimal RFC4180-ish streaming parser with multiline support.
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
                // swallow optional \n
                if (reader.Peek() == '\n')
                    reader.Read();

                record.Add(field.ToString());
                field.Clear();

                // ignore empty trailing lines
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

        // last record
        if (inQuotes)
        {
            // malformed CSV: treat as last field
            inQuotes = false;
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

            // preserve first occurrence
            if (!map.ContainsKey(key))
                map[key] = i;
        }
        return map;
    }

    private static string? Get(IReadOnlyList<string> row, Dictionary<string, int> map, params string[] names)
    {
        foreach (var n in names)
        {
            var key = NormalizeHeader(n);
            if (!map.TryGetValue(key, out var idx))
                continue;

            if (idx < 0 || idx >= row.Count)
                continue;

            var v = row[idx];
            if (v == null)
                return null;
            var trimmed = v.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        return null;
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

    private static void ApplyIfNonEmpty(Action apply, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            apply();
    }

    private static string? NullIfBlank(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        return t.Length == 0 ? null : t;
    }

    private static string? FormatDecimal(decimal v) => v.ToString(CultureInfo.InvariantCulture);

    private static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();

        // accept both 1,23 and 1.23
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;

        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out value))
            return true;

        // fallback: normalize commas to dots
        s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseInt(string? raw, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
               || int.TryParse(s, NumberStyles.Integer, CultureInfo.GetCultureInfo("it-IT"), out value);
    }

    private static bool TryParseBool(string? raw, out bool value)
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

    private static bool TryParseDate(string? raw, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value)
               || DateTime.TryParse(s, CultureInfo.GetCultureInfo("it-IT"), DateTimeStyles.AssumeLocal, out value);
    }
}
