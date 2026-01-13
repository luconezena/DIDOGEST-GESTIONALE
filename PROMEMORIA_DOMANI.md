# DIDO-GEST — Promemoria (09/01/2026)

## Stato generale (decisione chiave)
- Obiettivo: gestionale **single‑PC portable** (nessun servizio esterno, nessun server in rete).
- Database: **SQLite locale**.
- Fatturazione elettronica: **in pausa**.
- Build: `Release` compila con 0 errori; smoke test automatici disponibili.

## Dove siamo (riassunto rispetto a `progetto gestionale.txt`)
- Le macro‑aree ci sono (Documenti/Magazzino/Contabilità base/Assistenze/Archivio) e l’area **Documenti** è stata portata a un flusso reale.
- Focus attuale: **affidabilità** e uso reale su 1 PC (zero sorprese: numerazioni, incassi, backup, coerenza DB).

## Cosa è già fatto (ultimi blocchi)

### Migrazione dati (CSV)
- Export/Import CSV per anagrafiche principali (clienti/fornitori/articoli) + “pacchetto migrazione” multi‑CSV.
- Import con controlli (DB non vuoto → conferma) e ordinamento di caricamento per evitare FK/associazioni incoerenti.

### Localizzazione (italiano)
- Passata completa su UI/servizi/tools: popup e messaggi in italiano.

### Incassi (flusso reale)
- Aggiunto `DataPagamento` end‑to‑end (modello + DB + editor + liste).
- Scadenzario incassi + solleciti + stampa incassi periodo (anche da Fatture) con filtri per periodo.
- Hardening DB: se `Pagato=1` e `DataPagamento` NULL (DB vecchi) → riallineamento automatico per fatture.

### Numerazioni e integrità
- Anti‑duplicato numerazione documenti: se il numero “atteso” è già preso, incrementa fino a trovare un libero.
- Diagnostica duplicati su `(TipoDocumento, NumeroDocumento)` in log (best‑effort).

### Test automatici
- `Tools/DbSmokeTest` copre: schema/micro‑migrazioni + movimenti/giacenze + incassi (hardening+filtro) + numerazioni.
- Modalità SQL Server esiste ma **non è prioritaria** in single‑PC (rimane opzionale via env var).

### Backup (single‑PC)
- Menu Backup: backup SQLite ora è **snapshot consistente** (non semplice copy file).
- Rotazione automatica backup: mantiene gli ultimi **30** file in `Backup\`.

### Stabilità operativa (portable) + base per futura installabile
- Logging UI centralizzato: `UiLog.Error(...)` scrive in `Logs\ui-errors-YYYYMMDD.log`.
- Check avvio SQLite (fail-fast con messaggio chiaro):
	- cartella DB non scrivibile
	- file DB in sola lettura
	- DB bloccato/in uso (altra istanza, OneDrive/rete, antivirus/backup)
- Percorsi centralizzati: `AppPaths` (oggi: tutto accanto all'eseguibile; domani: facile puntare a cartella utente).

### Fatturazione elettronica (feature flag)
- Impostazione: **Abilita fatturazione elettronica**.
- Modalità:
	- **Commercialista**: salva XML in una cartella configurata (con pulsante “Sfoglia…”).
	- **Server**: invia XML via HTTP POST a endpoint esterno (token Bearer).
- Menu FE “gated”: se disabilitata non si entra nel modulo.

### Help in-app
- Menu: **AIUTO → Guida del programma**.
- La guida legge `HELP.md` dalla cartella dell’eseguibile (gestito anche in publish single-file).

### Distribuzione portable (cartella + zip)
- Publish self‑contained/single‑file e pacchetto distribuibile:
	- `portable\dist\DidoGest-Portable\` (cartella)
	- `portable\dist\DidoGest-Portable.zip` (zip)
- Per comodità viene copiato anche `portable\DidoGest.exe` e `portable\HELP.md` (lanciando l'exe in root l'Help funziona).
- Script unico: `MAKE_PORTABLE.ps1`.

### Fix UI login
- Allargati i campi “Utente/Password” nel login (e finestre cambio/reset password) per migliore leggibilità.

### Build pulita
- Warning `NETSDK1206` (SQLitePCLRaw alpine RID) silenziato nel progetto dati per avere build/publish puliti su target Windows.

## Promemoria tecnico — Portatile oggi, installabile domani
- **Portatile (oggi)**: si assume che `DidoGest.db`, `DidoGest.settings.json` e `Logs\` stiano accanto all'eseguibile.
- **Installabile (futura)**: il comportamento consigliato è eseguibile in Program Files e dati in cartella utente.
- Punto unico da toccare quando faremo l'installabile: `DidoGest.Data/AppPaths.cs`.
	- Qui si potrà spostare `SettingsPath`, `DefaultDatabasePath` e `LogsDirectory` verso `%LOCALAPPDATA%\DidoGest\...`.

## Checklist rapida (prossima sessione)

### 1) Collaudo incassi (10 minuti)
- Crea 2 fatture, marca una pagata oggi e una pagata con data fuori periodo.
- Verifica filtro “Pagate” + periodo (deve includere/escludere correttamente).
- Stampa “Incassi periodo” e verifica totali.

### 2) Collaudo backup (5 minuti)
- Utility → Backup: crea 2–3 backup.
- Verifica che i file siano in `Backup\` e che, superando 30, i più vecchi vengano rimossi.

### 3) Collaudo numerazioni (5 minuti)
- Crea più documenti dello stesso tipo nello stesso anno.
- Verifica che non si creino duplicati anche se un numero era già presente.

## Prossimi step (ordinati per impatto su single‑PC)

### A) Stabilità operativa (alta priorità)
- Aggiornare `PROVE_RAPIDE.md` aggiungendo i test “Incassi + Backup” (checklist ripetibile).
- (Fatto) Check all’avvio SQLite: cartella non scrivibile / DB sola lettura / DB bloccato, con messaggi guidati.

### B) Documenti: rifiniture pratiche (media priorità)
- Consolidare regole e UI per `FATTURA` / `FATTURA_ACCOMPAGNATORIA` / `DDT` (solo ciò che serve davvero all’uso).
- Migliorare le liste/stati (fatturato, pagato, scadenza) con coerenza e senza ambiguità.

### C) Magazzino: minimo indispensabile (media priorità)
- Verifica completa del flusso: vendita scarico / acquisto carico / conversioni dove presenti.

---

Nota: l’obiettivo “ReadyPro‑like” resta a lungo raggio; in single‑PC conviene chiudere prima robustezza + flussi reali (Documenti→Incassi→Backup) e solo dopo espandere moduli.
