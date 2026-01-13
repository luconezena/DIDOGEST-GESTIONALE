# DIDO-GEST - Gestionale Professionale

## Versione 1.0

**DIDO-GEST** è un gestionale completo e professionale per Windows, progettato per gestire tutte le principali procedure di gestione dell'attività commerciale.

Nota: per lo stato reale di implementazione (FATTO/PARZIALE/NON PRESENTE) vedi **REQUISITI_E_COPERTURA.md**.

---

## 🚀 Caratteristiche Principali

### 📦 Magazzino e Fatturazione
- **Anagrafiche articoli dettagliate** con gestione taglie, colori, numeri di serie
- **Gestione illimitata di listini** (fino a 10.000)
- **Gestione multipla di magazzini** (fino a 200)
- **Emissione documenti**: preventivi, ordini, DDT, fatture accompagnatorie e differite
- **Gestione codici a barre** e stampa etichette
- Formati italiani coerenti in tutta la UI (€, date e decimali)
- **Reverse charge** e gestione agenti
- **Statistiche** e visualizzazione giacenze in tempo reale

### ⚡ Fatturazione Elettronica
- **Stato attuale**: area in sviluppo / **in pausa** (vedi **REQUISITI_E_COPERTURA.md** per la copertura reale)
- Export XML **minimale** (non garantito conforme al tracciato FatturaPA)
- Firma digitale, invio/ricezione SDI e conservazione sostitutiva: **non presenti** in questa versione

### 💰 Contabilità Base
- **Piano dei conti** personalizzabile multilivello (2-8 livelli)
- **Prima nota** con partita doppia
- **Mastrini** clienti, fornitori e conti generici
- **Registri IVA** (acquisti, vendite, corrispettivi)
- Gestione IVA detraibile/indetraibile ed esigibilità differita
- **Riepilogo IVA** periodico

### 🔧 Assistenze, Cantieri e Contratti
- **Schede assistenza/riparazione** con workflow completo
- Gestione numeri di serie, lotti e codici IMEI
- **Contratti** a tempo determinato o a monte ore
- **Gestione cantieri** con costi manodopera e materiali
- Tracciamento interventi giornalieri per operaio
- Report valorizzati per margini e ricavi

### 📂 Archiviazione Documentale
- Archiviazione illimitata di file (PDF, Word, Excel, immagini, ecc.)
- Acquisizione diretta da scanner
- Collegamento documenti a clienti/fornitori/articoli
- Sistema di protocollo integrato
- Gestione RNC (Rapporti di Non Conformità) e Azioni Correttive
- Valutazione fornitori automatica
- Manuale della qualità integrato

---

## 📋 Requisiti di Sistema

### Requisiti Minimi
- **Sistema Operativo**: Windows 10 o superiore (64-bit)
- **RAM**: 4 GB (8 GB consigliati)
- **Spazio su disco**: 500 MB per l'installazione + spazio per database
- **.NET**: .NET 8.0 Runtime o superiore
- **Risoluzione schermo**: 1280x720 (1920x1080 consigliata)

### Database
Il software supporta:
- **SQLite** (incluso, nessuna configurazione richiesta) - Ideale per installazioni singole
- **SQL Server** 2019 o superiore - Consigliato per installazioni multi-utente

---

## 🔧 Installazione

### Installazione Rapida (SQLite - Consigliata per iniziare)

1. **Prerequisiti**:
   ```powershell
   # Verifica che .NET 8.0 sia installato
   dotnet --version
   
   # Se non installato, scaricalo da: https://dotnet.microsoft.com/download/dotnet/8.0
   ```

2. **Clona o scarica il progetto**:
   ```powershell
   git clone [repository-url]
   cd DIDOGEST
   ```

3. **Compila il progetto**:
   ```powershell
   dotnet restore
   dotnet build
   ```

4. **Avvia l'applicazione**:
   ```powershell
   cd ..\DidoGest.UI
   dotnet run
   ```

Il database viene creato automaticamente al primo avvio (e lo schema viene aggiornato tramite micro-migrazioni idempotenti).

### Installazione con SQL Server (Multi-utente)

Questa modalità è consigliata quando l'app viene usata da più PC in LAN (PC “server” + client), senza servizi esterni.

1. Installa SQL Server 2019+ (spesso basta SQL Server Express) sul PC che fa da “server” in rete
2. Configura la connessione in `DidoGest.settings.json` (generato dall'app) tramite **Utility → Impostazioni → Database**
3. Riavvia l'app: il DB e lo schema vengono creati/aggiornati automaticamente (EnsureCreated + micro-migrazioni)

Esempio minimale (Windows Auth):
```json
{
   "DatabaseProvider": "SqlServer",
   "SqlServerConnectionString": "Server=NOMEPCSERVER\\SQLEXPRESS;Database=DidoGest;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
}
```

Nota: se l'utente SQL non ha permessi di creazione DB, crea prima il database `DidoGest` e assegna i permessi.

---

## 📖 Guida Rapida

### Primo Avvio

Al primo avvio, il database verrà creato automaticamente con:
- Un magazzino principale (MAG01)
- Un listino base
- Struttura completa delle tabelle

### Operazioni Base

Suggerimento UX: nella maggior parte delle liste, **doppio click sulla riga** = apri/modifica (equivalente al pulsante "Modifica"/"Apri").

#### 1️⃣ Configurare le Anagrafiche
- Vai su **Anagrafiche → Clienti** per inserire i clienti
- Vai su **Anagrafiche → Fornitori** per inserire i fornitori
- Vai su **Anagrafiche → Agenti** per inserire gli agenti commerciali

#### 2️⃣ Configurare il Magazzino
- Vai su **Magazzino → Articoli** per inserire gli articoli
- Configura i listini prezzi in **Magazzino → Listini Prezzi**
- Esegui i carichi iniziali in **Magazzino → Movimenti**

#### 3️⃣ Emettere Documenti
- **Preventivi**: Documenti → Preventivi → Nuovo
- **Ordini**: Documenti → Ordini → Nuovo
- **DDT**: Documenti → DDT → Nuovo (scarica automaticamente il magazzino)
- **Fatture**: Documenti → Fatture → Nuovo

#### 4️⃣ Fatturazione Elettronica
- Vai su **Fatturazione Elettronica → Fatture Elettroniche**
- Seleziona una fattura e clicca su **Genera XML**
- Il sistema genera, firma e invia automaticamente tramite SDI

#### 5️⃣ Contabilità
- Registra le operazioni in **Contabilità → Prima Nota**
- Consulta i registri IVA in **Contabilità → Registri IVA**
- Visualizza i mastrini in **Contabilità → Mastrini**

---

## 🏗️ Architettura del Progetto

```
DIDOGEST/
│
├── DidoGest.UI/              # Interfaccia utente WPF
│   ├── Views/                # Finestre e controlli utente
│   ├── MainWindow.xaml       # Finestra principale
│   └── App.xaml              # Configurazione applicazione
│
├── DidoGest.Core/            # Logica di business ed entità
│   ├── Entities/             # Modelli di dominio
│   │   ├── Cliente.cs
│   │   ├── Fornitore.cs
│   │   ├── Articolo.cs
│   │   ├── Documento.cs
│   │   └── ...
│   └── Services/             # Servizi business logic
│       ├── MagazzinoService.cs
│       ├── DocumentoService.cs
│       ├── ContabilitaService.cs
│       └── FatturaElettronicaService.cs
│
└── DidoGest.Data/            # Accesso ai dati
    ├── DidoGestDbContext.cs  # Entity Framework DbContext
   ├── SqliteSchemaMigrator.cs      # Micro-migrazioni SQLite (idempotenti)
   └── SqlServerSchemaMigrator.cs   # Micro-migrazioni SQL Server (idempotenti)
```

---

## 🔐 Sicurezza e Backup

### Backup del Database

#### SQLite
Il file database `DidoGest.db` si trova nella cartella principale dell'applicazione.
Il modo consigliato per effettuare un backup è usare la funzione interna dell'app (**Utility → Backup database**).

- Il backup viene creato come snapshot consistente del DB (sicuro anche con app aperta)
- I backup vengono salvati in `Backup\` nella cartella dell'app
- L'app mantiene automaticamente gli ultimi 30 backup (i più vecchi vengono rimossi)

```powershell
# Backup manuale (solo a app CHIUSA)
Copy-Item "DidoGest.db" "C:\Backup\DidoGest_$(Get-Date -Format 'yyyyMMdd_HHmmss').db"
```

#### SQL Server
Utilizza gli strumenti nativi di SQL Server Management Studio o comandi T-SQL:
```sql
BACKUP DATABASE DidoGest 
TO DISK = 'C:\Backup\DidoGest.bak'
WITH FORMAT, COMPRESSION;
```

### Conservazione Sostitutiva
- In questa versione la conservazione sostitutiva automatica **non è inclusa**.
- Se usi export XML (area in sviluppo), i file vengono salvati in `FattureElettroniche/`.
- Per l'uso reale, prevedi un flusso esterno conforme (conservatore accreditato) e backup periodici.

---

## 📊 Database Schema

Il database include le seguenti entità principali:

**Anagrafiche**: Clienti, Fornitori, Agenti, Articoli
**Magazzino**: Magazzini, Giacenze, Movimenti, Listini
**Documenti**: Documenti, DocumentiRighe, Ordini, OrdiniRighe
**Contabilità**: PianoDeiConti, RegistrazioniContabili, MovimentiContabili, RegistriIVA
**Assistenze**: SchedeAssistenza, Interventi, Contratti, Cantieri
**Archivio**: DocumentiArchivio

---

## 🛠️ Personalizzazione

### Report Personalizzati
In questa versione sono inclusi modelli e risorse di stampa nelle cartelle del progetto (es. `Stampe/`, `Modelli/`).

### Campi Personalizzabili
Molte entità supportano campi personalizzabili per adattarsi alle specifiche esigenze aziendali.

---

## 📞 Supporto

### Documentazione
- Installazione: vedi `INSTALL.md`
- Note rapide: vedi `LEGGIMI.txt`
- FAQ/Video tutorial: non inclusi in questa versione

### Supporto Tecnico
- Email: support@didogest.com
- Orari: Lun-Ven 9:00-18:00

---

## 📝 Licenza

Copyright © 2025 DIDO Software. Tutti i diritti riservati.

Questo software è proprietario. È vietata la distribuzione, modifica o uso commerciale senza autorizzazione scritta.

---

## 🔄 Aggiornamenti

### Versione 1.0 (31/12/2025)
- Release iniziale
- Tutti i moduli base implementati
- Supporto fatturazione elettronica
- Architettura completa e scalabile

### Roadmap Futura
- [ ] Interfacce web responsive
- [ ] App mobile per agenti
- [ ] Integrazione e-commerce
- [ ] Business Intelligence avanzata
- [ ] API REST per integrazioni
- [ ] Modulo CRM avanzato

---

## 👥 Contributori

- Sviluppo: DIDO Software Team
- Design UI/UX: DIDO Software Team
- Testing: DIDO Software Team
- Documentazione: DIDO Software Team

---

## ⚠️ Note Importanti

1. **Fatturazione Elettronica**: configurare i dati di trasmissione e gli eventuali certificati/credenziali richiesti dal vostro flusso
2. **Codice destinatario/PEC**: da configurare in fase di setup (se applicabile)
3. **Backup**: Effettuare backup regolari del database (consigliato giornaliero)
4. **Aggiornamenti Windows**: Mantenere Windows aggiornato per la sicurezza
5. **Normativa**: verificare requisiti fiscali e adeguamenti normativi in base al vostro caso d’uso

---

**Buon lavoro con DIDO-GEST!** 🎉
