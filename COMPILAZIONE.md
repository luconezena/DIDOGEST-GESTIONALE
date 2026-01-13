# 🚀 Guida Rapida alla Compilazione

## Compilazione Automatica (CONSIGLIATO)

### Su Windows

1. **Doppio click su `BUILD.bat`**

   Lo script automatico:
   - Verifica che .NET SDK sia installato
   - Scarica le dipendenze
   - Compila il progetto
   - Crea l'eseguibile standalone

2. **Se .NET SDK non è installato**

   Lo script aprirà automaticamente il browser per scaricare .NET 8.0 SDK:
   https://dotnet.microsoft.com/download/dotnet/8.0

   Dopo l'installazione, riavvia `BUILD.bat`

3. **Risultato**

   Troverai la distribuzione portable in:
   ```
   D:\DIDOGEST\portable\dist\DidoGest-Portable\DidoGest.exe
   ```

   E anche un exe “comodo” in root:
   ```
   D:\DIDOGEST\portable\DidoGest.exe
   ```

---

## Compilazione Manuale

Se preferisci compilare manualmente:

```powershell
# 1. Apri PowerShell nella cartella D:\DIDOGEST

# 2. Restore dipendenze
dotnet restore

# 3. Build
dotnet build --configuration Release

# 4. Publish + pacchetto portable (consigliato)
powershell -NoProfile -ExecutionPolicy Bypass -File .\MAKE_PORTABLE.ps1
```

Nota:
- Se il publish fallisce per file in uso, chiudi DIDO-GEST oppure termina il processo prima di ripubblicare.
   ```powershell
   Get-Process | Where-Object { $_.ProcessName -like 'DidoGest*' } | Stop-Process -Force -ErrorAction SilentlyContinue
   ```
- La build Release è mantenuta senza warning bloccanti; eventuali warning transitive sono gestiti per tenere pulito l'output.

---

## Smoke test automatici (affidabilità)

Lo script `BUILD.bat` esegue automaticamente un **DbSmokeTest** (SQLite) per intercettare regressioni su:
- schema/micro-migrazioni
- logica movimenti/giacenze
- incassi (`Pagato`/`DataPagamento`) e filtro per periodo
- numerazioni anti-duplicato

### Smoke test SQL Server (opzionale)

Se vuoi testare anche SQL Server, imposta una variabile ambiente con la connection string e rilancia la build o il test:

```powershell
$env:DIDOGEST_SMOKE_SQLSERVER = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet run --project .\Tools\DbSmokeTest\DbSmokeTest.csproj -c Release
```

Note:
- Il test crea un database temporaneo `DidoGestSmoke_...`, esegue i controlli e poi lo elimina.
- Serve un utente con permessi di `CREATE DATABASE` / `DROP DATABASE` sul server.

---

## Creare un Installer Professionale

### Opzione 1: Inno Setup (GRATUITO - Consigliato)

1. **Scarica Inno Setup**
   https://jrsoftware.org/isinfo.php

2. **Compila prima il progetto**
   Esegui `BUILD.bat`

3. **Crea l'installer**
   - Apri `Setup-InnoSetup.iss` con Inno Setup
   - Click su Build → Compile
   - L'installer sarà creato in `D:\DIDOGEST\Installer\`

4. **Risultato**
   File: `DidoGest-Setup-v1.0.0.exe` (installer completo)

### Opzione 2: ClickOnce (Integrato in Visual Studio)

1. Apri il progetto in Visual Studio
2. Click destro su DidoGest.UI → Publish
3. Segui la procedura guidata
4. Genera l'installer

### Opzione 3: WiX Toolset

Per installer MSI professionali:
https://wixtoolset.org/

---

## Distribuzione

### Eseguibile Portable (No Installazione)

Copia l'intera cartella `portable\dist\DidoGest-Portable` su:
- Chiavetta USB
- Cartella locale (consigliato)
- Qualsiasi PC Windows 10/11

L'eseguibile funziona senza installazione!

Nota:
- Se usi SQLite (default), **non** mettere `DidoGest.db` su cartelle condivise di rete.
- Per uso multi-PC in LAN, configura SQL Server.

### Installer Completo

Distribuisci il file `DidoGest-Setup-v1.0.0.exe`:
- Gestisce l'installazione automatica
- Crea collegamenti nel menu Start
- Verifica i prerequisiti (.NET)
- Gestisce la disinstallazione

---

## Requisiti di Sistema

### Per Compilare
- Windows 10/11
- .NET 8.0 SDK
- 2 GB RAM
- 1 GB spazio disco

### Per Eseguire
- Windows 10/11 (64-bit)
- 4 GB RAM (8 GB consigliati)
- 500 MB spazio disco
- .NET 8.0 Runtime (incluso in build standalone)

---

## Risoluzione Problemi

### "dotnet comando non riconosciuto"
**Soluzione**: Installa .NET 8.0 SDK da https://dotnet.microsoft.com/download/dotnet/8.0

### "Errore durante la compilazione"
**Soluzione**: 
1. Elimina le cartelle `bin` e `obj`
2. Esegui `dotnet clean`
3. Ricompila

### "File DLL mancanti"
**Soluzione**: Usa `--self-contained true` nel comando publish

### "Errore Material Design"
**Soluzione**:
```powershell
dotnet restore --force
dotnet build --no-incremental
```

---

## File Generati

Dopo la compilazione, la cartella `portable\dist\DidoGest-Portable` contiene:

```
DidoGest\
├── DidoGest.exe          ← ESEGUIBILE PRINCIPALE
├── DidoGest.dll
├── DidoGest.pdb
├── *.dll                 (dipendenze)
├── README.md
├── HELP.md
├── LICENSE.txt
├── LEGGIMI.txt
└── [cartelle dati]
    ├── FattureElettroniche\
    ├── Archivio\
    ├── Backup\
    └── Logs\
```

**Dimensione totale**: ~150-200 MB (include tutti i runtime)

---

## Test dell'Eseguibile

Prima di distribuire:

1. **Test su macchina pulita**
   - VM Windows 10/11 senza .NET installato
   - Verifica che tutto funzioni

2. **Test funzionalità base**
   - Avvio applicazione
   - Creazione database
   - Apertura moduli principali

3. **Test prestazioni**
   - Tempo di avvio
   - Utilizzo memoria
   - Operazioni su database

---

## Supporto

Per problemi durante la compilazione:
- Email: support@didogest.com
- Documentazione: README.md
- FAQ: INSTALL.md

---

**Pronto per iniziare?**

Doppio click su **BUILD.bat** e in pochi minuti avrai il tuo eseguibile! 🎉
