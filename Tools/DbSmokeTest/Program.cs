using System;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DidoGest.Data;
using DidoGest.Data.Services;
using DidoGest.Core.Entities;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Exec(SqliteConnection conn, string sql)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
}

static object? Scalar(SqliteConnection conn, string sql)
{
    using var cmd = conn.CreateCommand();
    cmd.CommandText = sql;
    return cmd.ExecuteScalar();
}

static void AssertSqliteCheckOk(SqliteHealthCheckResult result, string context)
{
    if (!result.Ok)
    throw new InvalidOperationException($"ERRORE: {context}: {result.Issue} - {result.Details}");
}

static async Task<int> TryRunSqlServerSmokeAsync()
{
    var cs = (Environment.GetEnvironmentVariable("DIDOGEST_SMOKE_SQLSERVER") ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(cs))
    {
        Console.WriteLine("SALTA: test rapido SQL Server (variabile d'ambiente DIDOGEST_SMOKE_SQLSERVER non impostata).");
        return 0;
    }

    var tempDb = $"DidoGestSmoke_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}";

    SqlConnectionStringBuilder baseBuilder;
    try
    {
        baseBuilder = new SqlConnectionStringBuilder(cs);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERRORE: stringa di connessione SQL Server non valida: {ex.Message}");
        return 1;
    }

    // Connessione admin (master) per CREATE/DROP.
    var masterBuilder = new SqlConnectionStringBuilder(baseBuilder.ConnectionString)
    {
        InitialCatalog = "master"
    };

    // Connessione al DB temporaneo.
    var tempBuilder = new SqlConnectionStringBuilder(baseBuilder.ConnectionString)
    {
        InitialCatalog = tempDb
    };

    try
    {
        await using (var conn = new SqlConnection(masterBuilder.ConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{tempDb}]";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<DidoGestDbContext>()
            .UseSqlServer(tempBuilder.ConnectionString)
            .Options;

        await using (var ctx = new DidoGestDbContext(options))
        {
            // Crea schema EF e applica micro-migrazioni idempotenti (come l'app).
            await ctx.Database.EnsureCreatedAsync();
            SqlServerSchemaMigrator.EnsureSchema(ctx);

            // Dati minimi per test logiche (incassi + numerazione).
            var mag = new Magazzino { Codice = "MAGSMOKE", Descrizione = "Magazzino smoke", Principale = true, Attivo = true };
            ctx.Magazzini.Add(mag);

            var cliente = new Cliente
            {
                Codice = "CLISMOKE",
                RagioneSociale = "Cliente smoke",
                Attivo = true,
                DataCreazione = DateTime.Now
            };
            ctx.Clienti.Add(cliente);

            await ctx.SaveChangesAsync();

            var fattHard = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = "FATSMOKE-HARD",
                DataDocumento = DateTime.Today.AddDays(-10),
                ClienteId = cliente.Id,
                MagazzinoId = mag.Id,
                Pagato = true,
                DataPagamento = null,
                Imponibile = 100m,
                IVA = 22m,
                Totale = 122m
            };
            ctx.Documenti.Add(fattHard);
            await ctx.SaveChangesAsync();

            DataHardeningService.EnsureDataPagamentoForFatturePagate(ctx);
            var reloadedHard = await ctx.Documenti.AsNoTracking().FirstAsync(d => d.Id == fattHard.Id);
            Assert(reloadedHard.DataPagamento.HasValue, "ERRORE: DataPagamento non impostata dal hardening (SQL Server).");
            Assert(reloadedHard.DataPagamento!.Value.Date == fattHard.DataDocumento.Date,
                "ERRORE: DataPagamento attesa == DataDocumento per hardening (SQL Server).");

            var fattIn = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = "FATSMOKE-IN",
                DataDocumento = DateTime.Today.AddDays(-20),
                ClienteId = cliente.Id,
                MagazzinoId = mag.Id,
                Pagato = true,
                DataPagamento = DateTime.Today.AddDays(-3),
                Imponibile = 50m,
                IVA = 11m,
                Totale = 61m
            };
            var fattOut = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = "FATSMOKE-OUT",
                DataDocumento = DateTime.Today.AddDays(-30),
                ClienteId = cliente.Id,
                MagazzinoId = mag.Id,
                Pagato = true,
                DataPagamento = DateTime.Today.AddDays(-30),
                Imponibile = 70m,
                IVA = 15.4m,
                Totale = 85.4m
            };
            ctx.Documenti.AddRange(fattIn, fattOut);
            await ctx.SaveChangesAsync();

            var from = DateTime.Today.AddDays(-5).Date;
            var to = DateTime.Today.Date;
            var incassiNelPeriodo = await ctx.Documenti.AsNoTracking()
                .Where(d => d.Pagato && d.DataPagamento != null &&
                            d.TipoDocumento.Contains("FATTURA") &&
                            d.DataPagamento >= from && d.DataPagamento <= to)
                .ToListAsync();

            Assert(incassiNelPeriodo.Any(d => d.NumeroDocumento == "FATSMOKE-IN"),
                "ERRORE: fattura pagata nel periodo non trovata dal filtro incassi (SQL Server).");
            Assert(incassiNelPeriodo.All(d => d.NumeroDocumento != "FATSMOKE-OUT"),
                "ERRORE: fattura fuori periodo trovata dal filtro incassi (SQL Server).");

            var year = DateTime.Today.Year;
            ctx.Documenti.Add(new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = $"FAT{year}0002",
                DataDocumento = new DateTime(year, 1, 2),
                ClienteId = cliente.Id,
                MagazzinoId = mag.Id
            });
            await ctx.SaveChangesAsync();

            ctx.Documenti.Add(new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = $"FAT{year}0001",
                DataDocumento = new DateTime(year, 1, 3),
                ClienteId = cliente.Id,
                MagazzinoId = mag.Id
            });
            await ctx.SaveChangesAsync();

            var nextFatt = DocumentNumberService.GenerateNumeroDocumento(ctx, "FATTURA", new DateTime(year, 1, 4));
            Assert(nextFatt == $"FAT{year}0003", $"ERRORE: numerazione fattura attesa FAT{year}0003, trovata {nextFatt} (SQL Server).");

            Console.WriteLine("OK: test rapido SQL Server (hardening incassi + numerazioni).");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERRORE: test rapido SQL Server: {ex}");
        return 1;
    }
    finally
    {
        try
        {
            await using var conn = new SqlConnection(masterBuilder.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"IF DB_ID('{tempDb}') IS NOT NULL BEGIN " +
                $"ALTER DATABASE [{tempDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{tempDb}]; END";
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // best-effort: non bloccare il report dei test
        }
    }
}

var tempDir = Path.Combine(Path.GetTempPath(), "DidoGestSmoke");
Directory.CreateDirectory(tempDir);
var dbPath = Path.Combine(tempDir, $"smoke_{Guid.NewGuid():N}.db");

try
{
    // 1) Crea un DB "vecchio" minimale: Documenti SENZA MagazzinoId.
    using (var conn = new SqliteConnection($"Data Source={dbPath}") )
    {
        conn.Open();
        Exec(conn, "PRAGMA foreign_keys=OFF;");

        Exec(conn, "CREATE TABLE Magazzini (Id INTEGER PRIMARY KEY, Principale INTEGER NOT NULL DEFAULT 0);");
        Exec(conn, "INSERT INTO Magazzini (Id, Principale) VALUES (1, 0);");
        Exec(conn, "INSERT INTO Magazzini (Id, Principale) VALUES (5, 1);");

        Exec(conn, "CREATE TABLE Documenti (Id INTEGER PRIMARY KEY, TipoDocumento TEXT NULL, NumeroDocumento TEXT NULL);");
        Exec(conn, "INSERT INTO Documenti (Id, TipoDocumento, NumeroDocumento) VALUES (10, 'DDT', 'DDT20260001');");
        Exec(conn, "INSERT INTO Documenti (Id, TipoDocumento, NumeroDocumento) VALUES (11, 'DDT', 'DDT20260001');");
    }

    // 2) Esegue la micro-migrazione reale (quella usata dall'app).
    var options = new DbContextOptionsBuilder<DidoGestDbContext>()
        .UseSqlite($"Data Source={dbPath}")
        .Options;

    using (var ctx = new DidoGestDbContext(options))
    {
        SqliteSchemaMigrator.EnsureSchema(ctx);
    }

    // 2b) Health checks SQLite (accesso + lock)
    {
        var cs = $"Data Source={dbPath}";
        var ok = SqliteHealthChecks.CheckSqliteDatabase(dbPath, cs, includeLockCheck: true);
        AssertSqliteCheckOk(ok, "controllo stato SQLite (baseline)");

        using var lockConn = new SqliteConnection(new SqliteConnectionStringBuilder(cs)
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 1
        }.ToString());
        lockConn.Open();
        Exec(lockConn, "BEGIN IMMEDIATE;");

        var locked = SqliteHealthChecks.CheckSqliteDatabase(dbPath, cs, includeLockCheck: true);
        Assert(locked.Issue == SqliteStartupIssue.DatabaseLocked,
            $"ERRORE: controllo stato SQLite atteso DatabaseLocked, trovato {locked.Issue} ({locked.Details})");

        Exec(lockConn, "ROLLBACK;");

        Console.WriteLine("OK: controlli stato SQLite (baseline + rilevamento lock)." );
    }

    // 3) Verifica: colonna presente + valore riallineato a magazzino principale (5).
    using (var conn = new SqliteConnection($"Data Source={dbPath}"))
    {
        conn.Open();

        var hasColumn = false;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info('Documenti');";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var name = reader["name"]?.ToString();
                if (string.Equals(name, "MagazzinoId", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        Assert(hasColumn, "ERRORE: la colonna MagazzinoId non è stata aggiunta.");

        var magazzinoId = Scalar(conn, "SELECT MagazzinoId FROM Documenti WHERE Id = 10;");
        Assert(magazzinoId is not null, "ERRORE: MagazzinoId null sul record test.");

        var magazzinoIdInt = Convert.ToInt32(magazzinoId);
        Assert(magazzinoIdInt == 5, $"ERRORE: MagazzinoId atteso 5, trovato {magazzinoIdInt}.");

        // Indici: su DB vecchi possono esserci duplicati, ma EnsureSchema non deve crashare.
        // Verifica best-effort: esiste almeno un indice relativo a NumeroDocumento.
        var idxCountObj = Scalar(conn,
            "SELECT COUNT(*) FROM sqlite_master " +
            "WHERE type='index' AND tbl_name='Documenti' AND sql LIKE '%NumeroDocumento%';");
        var idxCount = idxCountObj is null ? 0 : Convert.ToInt32(idxCountObj);
        Assert(idxCount > 0, "ERRORE: nessun indice trovato su Documenti relativo a NumeroDocumento.");

        Console.WriteLine("OK: schema aggiornato e riallineato correttamente.");
    }

    // 4) Smoke test logica movimenti/giacenze su DB creato da EF (senza UI):
    //    crea documento con MagazzinoId=5 e verifica movimento su quel magazzino.
    var dbPath2 = Path.Combine(tempDir, $"smoke_logic_{Guid.NewGuid():N}.db");
    try
    {
        var options2 = new DbContextOptionsBuilder<DidoGestDbContext>()
            .UseSqlite($"Data Source={dbPath2}")
            .Options;

        using (var ctx = new DidoGestDbContext(options2))
        {
            ctx.Database.EnsureCreated();
            SqliteSchemaMigrator.EnsureSchema(ctx);

            // Magazzini minimi (per vincoli FK su Documento.MagazzinoId)
            var mag1 = new Magazzino { Codice = "MAG1", Descrizione = "Magazzino 1", Principale = true, Attivo = true };
            var mag2 = new Magazzino { Codice = "MAG2", Descrizione = "Magazzino 2", Principale = false, Attivo = true };
            ctx.Magazzini.Add(mag1);
            ctx.Magazzini.Add(mag2);

            // Articolo minimale
            var art = new Articolo
            {
                Codice = "ARTSMOKE",
                Descrizione = "Articolo smoke",
                Attivo = true,
                ArticoloDiServizio = false,
                PrezzoAcquisto = 10m,
                PrezzoVendita = 20m,
                AliquotaIVA = 22m,
                UnitaMisura = "PZ"
            };
            ctx.Articoli.Add(art);

            // Cliente minimale (vendita => SCARICO)
            var cliente = new Cliente
            {
                Codice = "CLISMOKE",
                RagioneSociale = "Cliente smoke",
                Attivo = true,
                DataCreazione = DateTime.Now
            };
            ctx.Clienti.Add(cliente);

            await ctx.SaveChangesAsync();

            var targetMagazzinoId = mag2.Id;

            var doc = new Documento
            {
                TipoDocumento = "DDT",
                NumeroDocumento = "DDTTEST",
                DataDocumento = DateTime.Today,
                MagazzinoId = targetMagazzinoId,
                ClienteId = cliente.Id,
                Imponibile = 0m,
                IVA = 0m,
                Totale = 0m,
                ScontoGlobale = 0m,
                SpeseAccessorie = 0m
            };
            ctx.Documenti.Add(doc);
            await ctx.SaveChangesAsync();

            ctx.DocumentiRighe.Add(new DocumentoRiga
            {
                DocumentoId = doc.Id,
                NumeroRiga = 1,
                ArticoloId = art.Id,
                Descrizione = art.Descrizione,
                Quantita = 2m,
                UnitaMisura = art.UnitaMisura,
                PrezzoUnitario = art.PrezzoVendita,
                PrezzoNetto = art.PrezzoVendita,
                AliquotaIVA = art.AliquotaIVA,
                Imponibile = 0m,
                ImportoIVA = 0m,
                Totale = 0m,
                RigaDescrittiva = false
            });
            await ctx.SaveChangesAsync();

            var service = new DocumentoMagazzinoService(ctx);
            await service.SyncMovimentiMagazzinoForDocumentoAsync(doc.Id, doc.TipoDocumento, doc.NumeroDocumento, doc.DataDocumento, doc.DocumentoOriginaleId);

            var mov = await ctx.MovimentiMagazzino.AsNoTracking().Where(m => m.DocumentoId == doc.Id).ToListAsync();
            Assert(mov.Count == 1, $"ERRORE: atteso 1 movimento, trovato {mov.Count}.");

            var firstMov = mov.Single();

            Assert(firstMov.MagazzinoId == targetMagazzinoId,
                $"ERRORE: MagazzinoId movimento atteso {targetMagazzinoId}, trovato {firstMov.MagazzinoId}.");

            Assert(string.Equals(firstMov.TipoMovimento, "SCARICO", StringComparison.OrdinalIgnoreCase),
                $"ERRORE: TipoMovimento atteso SCARICO, trovato {firstMov.TipoMovimento}.");

            var giac = await ctx.GiacenzeMagazzino.AsNoTracking().FirstOrDefaultAsync(g => g.MagazzinoId == targetMagazzinoId && g.ArticoloId == art.Id);
            Assert(giac != null, "ERRORE: giacenza non creata.");

            var giacOk = giac!;
            Assert(giacOk.Quantita == -2m, $"ERRORE: giacenza attesa -2, trovata {giacOk.Quantita}.");

            Console.WriteLine("OK: movimenti/giacenze coerenti col magazzino del documento.");

            // 5) Smoke test incassi: hardening DataPagamento e filtro periodo.
            var fattHard = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = "FATSMOKE-HARD",
                DataDocumento = DateTime.Today.AddDays(-10),
                ClienteId = cliente.Id,
                MagazzinoId = mag1.Id,
                Pagato = true,
                DataPagamento = null,
                Imponibile = 100m,
                IVA = 22m,
                Totale = 122m
            };
            ctx.Documenti.Add(fattHard);
            await ctx.SaveChangesAsync();

            DataHardeningService.EnsureDataPagamentoForFatturePagate(ctx);
            var reloadedHard = await ctx.Documenti.AsNoTracking().FirstAsync(d => d.Id == fattHard.Id);
            Assert(reloadedHard.DataPagamento.HasValue, "ERRORE: DataPagamento non impostata dal hardening su fattura pagata.");
            Assert(reloadedHard.DataPagamento!.Value.Date == fattHard.DataDocumento.Date,
                "ERRORE: DataPagamento attesa == DataDocumento per hardening.");

            var fattIn = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = "FATSMOKE-IN",
                DataDocumento = DateTime.Today.AddDays(-20),
                ClienteId = cliente.Id,
                MagazzinoId = mag1.Id,
                Pagato = true,
                DataPagamento = DateTime.Today.AddDays(-3),
                Imponibile = 50m,
                IVA = 11m,
                Totale = 61m
            };
            var fattOut = new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = "FATSMOKE-OUT",
                DataDocumento = DateTime.Today.AddDays(-30),
                ClienteId = cliente.Id,
                MagazzinoId = mag1.Id,
                Pagato = true,
                DataPagamento = DateTime.Today.AddDays(-30),
                Imponibile = 70m,
                IVA = 15.4m,
                Totale = 85.4m
            };
            ctx.Documenti.AddRange(fattIn, fattOut);
            await ctx.SaveChangesAsync();

            var from = DateTime.Today.AddDays(-5).Date;
            var to = DateTime.Today.Date;
            var incassiNelPeriodo = await ctx.Documenti.AsNoTracking()
                .Where(d => d.Pagato && d.DataPagamento != null &&
                            d.TipoDocumento.Contains("FATTURA") &&
                            d.DataPagamento >= from && d.DataPagamento <= to)
                .ToListAsync();

            Assert(incassiNelPeriodo.Any(d => d.NumeroDocumento == "FATSMOKE-IN"),
                "ERRORE: fattura pagata nel periodo non trovata dal filtro incassi.");
            Assert(incassiNelPeriodo.All(d => d.NumeroDocumento != "FATSMOKE-OUT"),
                "ERRORE: fattura fuori periodo trovata dal filtro incassi.");

            Console.WriteLine("OK: hardening DataPagamento e filtro incassi per periodo.");

            // 6) Smoke test numerazione: anti-duplicato (probe) per fatture e preventivi.
            var year = DateTime.Today.Year;
            ctx.Documenti.Add(new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = $"FAT{year}0002",
                DataDocumento = new DateTime(year, 1, 2),
                ClienteId = cliente.Id,
                MagazzinoId = mag1.Id
            });
            await ctx.SaveChangesAsync();

            ctx.Documenti.Add(new Documento
            {
                TipoDocumento = "FATTURA",
                NumeroDocumento = $"FAT{year}0001",
                DataDocumento = new DateTime(year, 1, 3),
                ClienteId = cliente.Id,
                MagazzinoId = mag1.Id
            });
            await ctx.SaveChangesAsync();

            var nextFatt = DocumentNumberService.GenerateNumeroDocumento(ctx, "FATTURA", new DateTime(year, 1, 4));
            Assert(nextFatt == $"FAT{year}0003", $"ERRORE: numerazione fattura attesa FAT{year}0003, trovata {nextFatt}.");

            ctx.Documenti.Add(new Documento
            {
                TipoDocumento = "PREVENTIVO",
                NumeroDocumento = "PRE000002",
                DataDocumento = new DateTime(year, 1, 1),
                ClienteId = cliente.Id,
                MagazzinoId = mag1.Id
            });
            await ctx.SaveChangesAsync();

            ctx.Documenti.Add(new Documento
            {
                TipoDocumento = "PREVENTIVO",
                NumeroDocumento = "PRE000001",
                DataDocumento = new DateTime(year, 1, 2),
                ClienteId = cliente.Id,
                MagazzinoId = mag1.Id
            });
            await ctx.SaveChangesAsync();

            var nextPre = DocumentNumberService.GenerateNumeroDocumento(ctx, "PREVENTIVO", new DateTime(year, 1, 3));
            Assert(nextPre == "PRE000003", $"ERRORE: numerazione preventivo attesa PRE000003, trovata {nextPre}.");

            Console.WriteLine("OK: numerazione anti-duplicato (probe) per fatture e preventivi.");

            // 7) Smoke test backup SQLite: snapshot consistente + pruning.
            var backupDir = Path.Combine(tempDir, $"backup_{Guid.NewGuid():N}");
            Directory.CreateDirectory(backupDir);

            var backupReal = Path.Combine(backupDir, $"DidoGest_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            SqliteBackupService.CreateBackup(dbPath2, backupReal);
            Assert(File.Exists(backupReal), "ERRORE: backup SQLite non creato.");

            // Verifica che il backup sia apribile e contenga dati minimi attesi.
            using (var bconn = new SqliteConnection($"Data Source={backupReal};Mode=ReadOnly"))
            {
                bconn.Open();
                var docs = Scalar(bconn, "SELECT COUNT(*) FROM Documenti;");
                var docsCount = docs is null ? 0 : Convert.ToInt32(docs);
                Assert(docsCount > 0, "ERRORE: backup SQLite creato ma sembra vuoto (Documenti=0)." );
            }

            // Crea backup "finti" (vuoti) per test pruning ordinato per timestamp nel nome.
            var fakeOld = Path.Combine(backupDir, "DidoGest_20000101_000000.db");
            var fakeMid = Path.Combine(backupDir, "DidoGest_20100101_000000.db");
            File.WriteAllText(fakeOld, string.Empty);
            File.WriteAllText(fakeMid, string.Empty);

            SqliteBackupService.PruneOldBackups(backupDir, keepLast: 2);

            var remaining = Directory.GetFiles(backupDir, "DidoGest_*.db", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            Assert(remaining.Count == 2, $"ERRORE: pruning backup atteso 2 file, trovati {remaining.Count}.");
            Assert(remaining.Any(n => string.Equals(n, Path.GetFileName(backupReal), StringComparison.OrdinalIgnoreCase)),
                "ERRORE: pruning backup ha eliminato il backup più recente.");

            // Cleanup best-effort
            try { Directory.Delete(backupDir, recursive: true); } catch { /* ignore */ }

            Console.WriteLine("OK: backup SQLite (snapshot + pruning).");
        }
    }
    finally
    {
        try { if (File.Exists(dbPath2)) File.Delete(dbPath2); } catch { /* ignore */ }
    }

    // 7) Smoke test SQL Server (opzionale via env var)
    var sqlServerResult = await TryRunSqlServerSmokeAsync();
    if (sqlServerResult != 0) return sqlServerResult;

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERRORE: eccezione: {ex}");
    return 1;
}
finally
{
    try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { /* ignore */ }
}
