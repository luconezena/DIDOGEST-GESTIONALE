using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data;

public static class SqlServerSchemaMigrator
{
    public static void EnsureSchema(DidoGestDbContext ctx)
    {
        // Nota: per SQL Server l'app usa EnsureCreated, che NON evolve lo schema.
        // Qui facciamo micro-migrazioni idempotenti come per SQLite.

        // Documenti.DataPagamento
        ctx.Database.ExecuteSqlRaw(
            "IF COL_LENGTH('Documenti', 'DataPagamento') IS NULL " +
            "BEGIN ALTER TABLE Documenti ADD DataPagamento datetime2 NULL; END");

        // Indice su (TipoDocumento, NumeroDocumento): idealmente UNIQUE, ma non blocchiamo su DB storici con duplicati.
        try
        {
            ctx.Database.ExecuteSqlRaw(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Documenti_TipoNumero' AND object_id = OBJECT_ID('Documenti')) " +
                "BEGIN " +
                "    IF NOT EXISTS (" +
                "        SELECT 1 FROM Documenti " +
                "        WHERE TipoDocumento IS NOT NULL AND NumeroDocumento IS NOT NULL " +
                "        GROUP BY TipoDocumento, NumeroDocumento HAVING COUNT(*) > 1" +
                "    ) " +
                "    BEGIN " +
                "        CREATE UNIQUE INDEX IX_Documenti_TipoNumero ON Documenti (TipoDocumento, NumeroDocumento); " +
                "    END " +
                "END");

            ctx.Database.ExecuteSqlRaw(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Documenti_TipoNumero_NU' AND object_id = OBJECT_ID('Documenti')) " +
                "BEGIN " +
                "    IF EXISTS (" +
                "        SELECT 1 FROM Documenti " +
                "        WHERE TipoDocumento IS NOT NULL AND NumeroDocumento IS NOT NULL " +
                "        GROUP BY TipoDocumento, NumeroDocumento HAVING COUNT(*) > 1" +
                "    ) " +
                "    BEGIN " +
                "        CREATE INDEX IX_Documenti_TipoNumero_NU ON Documenti (TipoDocumento, NumeroDocumento); " +
                "    END " +
                "END");
        }
        catch (Exception ex)
        {
            DataLog.Error("SqlServerSchemaMigrator.EnsureSchema", ex);
        }
    }
}
