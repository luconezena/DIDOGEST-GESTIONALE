using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data;

public static class DataHardeningService
{
    /// <summary>
    /// Idempotente: su DB vecchi può capitare Pagato=true ma DataPagamento=NULL.
    /// Per le fatture, riallinea DataPagamento a DataDocumento.
    /// </summary>
    public static void EnsureDataPagamentoForFatturePagate(DidoGestDbContext ctx)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        ctx.Database.ExecuteSqlRaw(
            "UPDATE Documenti SET DataPagamento = DataDocumento " +
            "WHERE Pagato = 1 AND DataPagamento IS NULL AND TipoDocumento LIKE '%FATTURA%';");
    }
}
