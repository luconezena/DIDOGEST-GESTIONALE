# DIDO-GEST - Note di Installazione

## Istruzioni di Build e Distribuzione

### Build per Rilascio

Per creare una versione eseguibile del software:

```powershell
# 1. Posizionati nella cartella della soluzione
cd D:\DIDOGEST

# 2. Pulisci eventuali build precedenti
dotnet clean

# 3. Build in modalità Release
dotnet build --configuration Release

# 4. Pubblica e crea la distribuzione portable (consigliato)
powershell -NoProfile -ExecutionPolicy Bypass -File .\MAKE_PORTABLE.ps1

# Output:
# - .\portable\DidoGest.exe           (exe “comodo” in root)
# - .\portable\HELP.md               (guida in-app accanto all'exe)
# - .\portable\dist\DidoGest-Portable\ (cartella distribuibile)
# - .\portable\dist\DidoGest-Portable.zip (zip distribuibile)
```

### Creazione Installer (Opzionale)

Per creare un installer professionale, puoi utilizzare:

1. **Inno Setup** (gratuito): https://jrsoftware.org/isinfo.php
2. **WiX Toolset** (gratuito): https://wixtoolset.org/
3. **Advanced Installer** (commerciale): https://www.advancedinstaller.com/

### Distribuzione

Distribuzione consigliata: copia la cartella `portable\dist\DidoGest-Portable` (o lo ZIP) su un PC Windows.

Nota: questa build è *self-contained* e non richiede .NET installato.

### Prima Configurazione

1. Modifica `App.config` con i dati della tua azienda
2. (Opzionale) Configura la fatturazione elettronica in **Utility → Impostazioni** (feature flag + modalità)
3. Configura il database (di default SQLite; per multi-PC in LAN consigliato SQL Server)
4. Esegui il primo avvio: il DB viene creato/aggiornato automaticamente (EnsureCreated + micro-migrazioni)

### Dati DEMO (opzionale)

Per test e prove rapide puoi abilitare i dati demo (idempotenti): l'app crea/aggiorna anagrafiche e documenti di esempio (clienti/fornitori/articoli/documenti e anche **agenti**).

- Da UI: **Utility → Impostazioni → Dati demo**
- Da file: in `DidoGest.settings.json` imposta `EnableDemoData` a `true`

Nota: i dati demo sono identificabili tramite codici con prefisso `DIDOGEST-DEMO-`.

### Configurazione Database

L'app salva le impostazioni in `DidoGest.settings.json` nella stessa cartella dell'eseguibile.

#### SQLite (default)
- Nessuna configurazione richiesta: viene creato `DidoGest.db` nella cartella dell'app.
- Per spostare il DB (es. su disco dati), imposta `PercorsoDatabase` in `DidoGest.settings.json`.

##### Backup (consigliato)
- Usa **Utility → Backup database** per creare uno snapshot consistente (sicuro anche con app aperta)
- I backup finiscono in `Backup\\` accanto all'eseguibile e l'app mantiene automaticamente gli ultimi 30
- Backup manuale: copia `DidoGest.db` **solo a app chiusa**

#### SQL Server (consigliato per LAN multi-PC)
- Imposta `DatabaseProvider=SqlServer` e una connection string valida.
- Il DB e lo schema vengono creati/aggiornati automaticamente all'avvio.
- Richiede permessi di creazione DB/schema (o DB pre-creato con permessi adeguati).

---

## Struttura Cartelle Runtime

L'applicazione crea automaticamente le seguenti cartelle:

```
[Cartella Applicazione]/
├── DidoGest.exe
├── DidoGest.db (database SQLite)
├── App.config
├── FattureElettroniche/    # XML fatture generate
├── Certificati/             # Certificati firma digitale
├── Archivio/                # Documenti archiviati
├── Modelli/                 # Template documenti
├── Stampe/                  # PDF generati
├── Logs/                    # File di log
└── Backup/                  # Backup database
```

---

## Risoluzione Problemi Comuni

### Problema: "Impossibile avviare l'applicazione"
**Soluzione**: Installa .NET 8.0 Runtime da https://dotnet.microsoft.com/download/dotnet/8.0

### Problema: "Impossibile connettersi al database"
**Soluzione**: Verifica i permessi di scrittura nella cartella dell'applicazione

### Problema: "Errore durante la generazione fattura elettronica"
**Soluzione**: il modulo di fatturazione elettronica è in sviluppo/in pausa in questa versione. Se stai testando l'export XML, verifica la configurazione in App.config e che i dati anagrafici siano completi.

### Problema: "Errore Material Design"
**Soluzione**: Reinstalla i pacchetti NuGet:
```powershell
dotnet restore --force
```

### Nota formati (valuta/date)
L'app forza la cultura `it-IT` all'avvio per mantenere formati coerenti (simbolo valuta `€`, date e separatori decimali) indipendentemente dalle impostazioni regionali del PC.

---

## Performance e Ottimizzazione

Per database di grandi dimensioni (>100.000 record):
1. Considera l'utilizzo di SQL Server invece di SQLite
2. Abilita gli indici nel database
3. Effettua manutenzione periodica del database

---

## Sicurezza

- Effettua backup regolari
- Proteggi il file database da accessi non autorizzati
- Non condividere il certificato digitale
- Mantieni aggiornato il software e Windows
- Utilizza password complesse per gli account

---

## Licenze Componenti Terze Parti

- **Entity Framework Core**: MIT License
- **Material Design In XAML**: MIT License
- **.NET Runtime**: MIT License

---

Per ulteriore supporto, consulta il file README.md o contatta il supporto tecnico.
