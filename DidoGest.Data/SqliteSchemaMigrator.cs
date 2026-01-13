using System;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace DidoGest.Data;

public static class SqliteSchemaMigrator
{
    public static void EnsureSchema(DidoGestDbContext ctx)
    {
        // Nota: la app usa EnsureCreated (no migrations). Qui facciamo “micro-migrazioni” idempotenti.
        // Gestiamo:
        // - Documenti.MagazzinoId
        // - Indici utili/di integrità (best-effort)
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('Documenti');";

        var hasMagazzinoId = false;
        var hasDataPagamento = false;
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var name = reader["name"]?.ToString();
                if (string.Equals(name, "MagazzinoId", StringComparison.OrdinalIgnoreCase))
                {
                    hasMagazzinoId = true;
                }

                if (string.Equals(name, "DataPagamento", StringComparison.OrdinalIgnoreCase))
                {
                    hasDataPagamento = true;
                }

                if (hasMagazzinoId && hasDataPagamento)
                    break;
            }
        }

        if (!hasMagazzinoId)
        {
            // Default 1 = Magazzino Principale seedato.
            ctx.Database.ExecuteSqlRaw("ALTER TABLE Documenti ADD COLUMN MagazzinoId INTEGER NOT NULL DEFAULT 1;");

            // Riallinea i record esistenti al magazzino principale (se diverso da 1).
            // Nota: su DB vecchi non esisteva alcuna scelta magazzino, quindi è sicuro mappare tutto al principale.
            int? magazzinoPrincipaleId = null;
            try
            {
                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT Id FROM Magazzini ORDER BY Principale DESC, Id ASC LIMIT 1;";
                var obj = cmd2.ExecuteScalar();
                if (obj != null && obj != DBNull.Value)
                {
                    magazzinoPrincipaleId = Convert.ToInt32(obj);
                }
            }
            catch (Exception ex)
            {
                // Se la tabella Magazzini non esiste o non è interrogabile, restiamo sul default 1.
                DataLog.Error("SqliteSchemaMigrator.EnsureSchema.ResolveMagazzinoPrincipale", ex);
                magazzinoPrincipaleId = null;
            }

            if (magazzinoPrincipaleId.HasValue && magazzinoPrincipaleId.Value != 1)
            {
                ctx.Database.ExecuteSqlRaw(
                    "UPDATE Documenti SET MagazzinoId = {0} WHERE MagazzinoId = 1;",
                    magazzinoPrincipaleId.Value);
            }
        }

        if (!hasDataPagamento)
        {
            // Data pagamento/incasso (nullable). SQLite tipizza liberamente: TEXT è coerente con mapping DateTime.
            ctx.Database.ExecuteSqlRaw("ALTER TABLE Documenti ADD COLUMN DataPagamento TEXT NULL;");
        }

        // Indice su (TipoDocumento, NumeroDocumento): se non ci sono duplicati, lo vogliamo UNIVOCO.
        // Se ci sono duplicati pregressi, creiamo solo un indice non-univoco (nome diverso) per performance, senza bloccare l'avvio.
        try
        {
            using var dupCmd = conn.CreateCommand();
            dupCmd.CommandText =
                "SELECT COUNT(*) FROM (" +
                "SELECT TipoDocumento, NumeroDocumento FROM Documenti " +
                "WHERE TipoDocumento IS NOT NULL AND NumeroDocumento IS NOT NULL " +
                "GROUP BY TipoDocumento, NumeroDocumento HAVING COUNT(*) > 1" +
                ") t;";

            var dupObj = dupCmd.ExecuteScalar();
            var dupCount = dupObj == null || dupObj == DBNull.Value ? 0 : Convert.ToInt32(dupObj);

            if (dupCount == 0)
            {
                // Se in passato è stato creato un indice non-univoco con questo scopo, lo rimuoviamo e mettiamo l'univoco.
                ctx.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Documenti_TipoNumero_NU;");
                ctx.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Documenti_TipoNumero;");
                ctx.Database.ExecuteSqlRaw(
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_Documenti_TipoNumero ON Documenti (TipoDocumento, NumeroDocumento);");
            }
            else
            {
                ctx.Database.ExecuteSqlRaw(
                    "CREATE INDEX IF NOT EXISTS IX_Documenti_TipoNumero_NU ON Documenti (TipoDocumento, NumeroDocumento);");
            }
        }
        catch (Exception ex)
        {
            DataLog.Error("SqliteSchemaMigrator.EnsureSchema.DocumentiIndexes", ex);
        }

        // Tabella collegamenti documenti (es. fattura differita che raggruppa più DDT)
        try
        {
            using var cmdTbl = conn.CreateCommand();
            cmdTbl.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='DocumentoCollegamenti';";
            var exists = cmdTbl.ExecuteScalar() != null;

            if (!exists)
            {
                ctx.Database.ExecuteSqlRaw(
                    "CREATE TABLE DocumentoCollegamenti (" +
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "DocumentoId INTEGER NOT NULL, " +
                    "DocumentoOrigineId INTEGER NOT NULL, " +
                    "FOREIGN KEY(DocumentoId) REFERENCES Documenti(Id) ON DELETE CASCADE, " +
                    "FOREIGN KEY(DocumentoOrigineId) REFERENCES Documenti(Id) ON DELETE CASCADE" +
                    ");");
            }

            // Indici best-effort
            ctx.Database.ExecuteSqlRaw(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_DocumentoCollegamenti_Doc_DocOrigine ON DocumentoCollegamenti (DocumentoId, DocumentoOrigineId);");
            ctx.Database.ExecuteSqlRaw(
                "CREATE INDEX IF NOT EXISTS IX_DocumentoCollegamenti_Origine ON DocumentoCollegamenti (DocumentoOrigineId);");
        }
        catch (Exception ex)
        {
            DataLog.Error("SqliteSchemaMigrator.EnsureSchema.DocumentoCollegamenti", ex);
        }

        // Tabella utenti applicazione (login locale) - best-effort
        try
        {
            using var cmdTbl = conn.CreateCommand();
            cmdTbl.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='UtentiSistema';";
            var exists = cmdTbl.ExecuteScalar() != null;

            if (!exists)
            {
                ctx.Database.ExecuteSqlRaw(
                    "CREATE TABLE UtentiSistema (" +
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "Username TEXT NOT NULL, " +
                    "PasswordHash BLOB NOT NULL, " +
                    "PasswordSalt BLOB NOT NULL, " +
                    "Ruolo TEXT NOT NULL, " +
                    "Attivo INTEGER NOT NULL DEFAULT 1, " +
                    "MustChangePassword INTEGER NOT NULL DEFAULT 1, " +
                    "CreatedAt TEXT NOT NULL, " +
                    "UpdatedAt TEXT NOT NULL" +
                    ");");
            }

            ctx.Database.ExecuteSqlRaw(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_UtentiSistema_Username ON UtentiSistema (Username);");
        }
        catch (Exception ex)
        {
            DataLog.Error("SqliteSchemaMigrator.EnsureSchema.UtentiSistema", ex);
        }
    }
}
