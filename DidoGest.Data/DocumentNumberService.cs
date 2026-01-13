using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data;

public static class DocumentNumberService
{
    private const int MaxProbeAttempts = 5000;

    public static async Task<string> GenerateNumeroDocumentoAsync(
        DidoGestDbContext ctx,
        string tipoDocumento,
        DateTime dataDocumento)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var year = dataDocumento.Year;
        var tipoUpper = (tipoDocumento ?? string.Empty).Trim().ToUpperInvariant();

        // Preventivi: numerazione storica PRE000001 (senza anno nel prefisso)
        if (tipoUpper == "PREVENTIVO")
        {
            var last = await ctx.Documenti
                .AsNoTracking()
                .Where(d => d.TipoDocumento == tipoDocumento)
                .OrderByDescending(d => d.Id)
                .Select(d => d.NumeroDocumento)
                .FirstOrDefaultAsync();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var digits = new string(last.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var n)) next = n + 1;
            }

            for (var i = 0; i < MaxProbeAttempts; i++)
            {
                var candidate = $"PRE{next:D6}";
                var exists = await ctx.Documenti
                    .AsNoTracking()
                    .AnyAsync(d => d.TipoDocumento == tipoDocumento && d.NumeroDocumento == candidate);

                if (!exists) return candidate;
                next++;
            }

            throw new InvalidOperationException("Impossibile generare un numero preventivo univoco (troppi tentativi).");
        }

        var prefix = tipoUpper switch
        {
            "DDT" => $"DDT{year}",
            "FATTURA_ACCOMPAGNATORIA" => $"FAC{year}",
            var t when t.Contains("FATTURA") => $"FAT{year}",
            _ => $"DOC{year}"
        };

        var lastByPrefix = await ctx.Documenti
            .AsNoTracking()
            .Where(d => d.TipoDocumento == tipoDocumento && d.NumeroDocumento.StartsWith(prefix))
            .OrderByDescending(d => d.Id)
            .Select(d => d.NumeroDocumento)
            .FirstOrDefaultAsync();

        var nextN = 1;
        if (!string.IsNullOrWhiteSpace(lastByPrefix) && lastByPrefix.Length >= prefix.Length)
        {
            var tail = lastByPrefix.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) nextN = n + 1;
        }

        for (var i = 0; i < MaxProbeAttempts; i++)
        {
            var candidate = $"{prefix}{nextN:D4}";
            var exists = await ctx.Documenti
                .AsNoTracking()
                .AnyAsync(d => d.TipoDocumento == tipoDocumento && d.NumeroDocumento == candidate);

            if (!exists) return candidate;
            nextN++;
        }

        throw new InvalidOperationException("Impossibile generare un numero documento univoco (troppi tentativi).");
    }

    // Variante sync per startup (OnStartup non-async)
    public static string GenerateNumeroDocumento(
        DidoGestDbContext ctx,
        string tipoDocumento,
        DateTime dataDocumento)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var year = dataDocumento.Year;
        var tipoUpper = (tipoDocumento ?? string.Empty).Trim().ToUpperInvariant();

        if (tipoUpper == "PREVENTIVO")
        {
            var last = ctx.Documenti
                .AsNoTracking()
                .Where(d => d.TipoDocumento == tipoDocumento)
                .OrderByDescending(d => d.Id)
                .Select(d => d.NumeroDocumento)
                .FirstOrDefault();

            var next = 1;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var digits = new string(last.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var n)) next = n + 1;
            }

            for (var i = 0; i < MaxProbeAttempts; i++)
            {
                var candidate = $"PRE{next:D6}";
                var exists = ctx.Documenti
                    .AsNoTracking()
                    .Any(d => d.TipoDocumento == tipoDocumento && d.NumeroDocumento == candidate);

                if (!exists) return candidate;
                next++;
            }

            throw new InvalidOperationException("Impossibile generare un numero preventivo univoco (troppi tentativi).");
        }

        var prefix = tipoUpper switch
        {
            "DDT" => $"DDT{year}",
            "FATTURA_ACCOMPAGNATORIA" => $"FAC{year}",
            var t when t.Contains("FATTURA") => $"FAT{year}",
            _ => $"DOC{year}"
        };

        var lastByPrefix = ctx.Documenti
            .AsNoTracking()
            .Where(d => d.TipoDocumento == tipoDocumento && d.NumeroDocumento.StartsWith(prefix))
            .OrderByDescending(d => d.Id)
            .Select(d => d.NumeroDocumento)
            .FirstOrDefault();

        var nextN = 1;
        if (!string.IsNullOrWhiteSpace(lastByPrefix) && lastByPrefix.Length >= prefix.Length)
        {
            var tail = lastByPrefix.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) nextN = n + 1;
        }

        for (var i = 0; i < MaxProbeAttempts; i++)
        {
            var candidate = $"{prefix}{nextN:D4}";
            var exists = ctx.Documenti
                .AsNoTracking()
                .Any(d => d.TipoDocumento == tipoDocumento && d.NumeroDocumento == candidate);

            if (!exists) return candidate;
            nextN++;
        }

        throw new InvalidOperationException("Impossibile generare un numero documento univoco (troppi tentativi).");
    }
}
