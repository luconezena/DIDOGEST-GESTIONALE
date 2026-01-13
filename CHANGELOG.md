# DIDO-GEST - Changelog

Nota: questo changelog riassume le modifiche principali, ma per la copertura reale (FATTO/PARZIALE/NON PRESENTE) fa fede **REQUISITI_E_COPERTURA.md**.

## [1.0.2] - 09/01/2026

### 📦 Distribuzione portable
- Pubblicazione self-contained/single-file e pacchetto distribuibile aggiornato (cartella + ZIP).
- Aggiunto script `MAKE_PORTABLE.ps1` per rigenerare rapidamente `portable\dist\DidoGest-Portable` e `DidoGest-Portable.zip`.

### ❓ Help in-app
- Nuovo menu **AIUTO → Guida del programma**.
- Finestra guida che carica `HELP.md` dalla cartella dell'eseguibile (robusta anche in publish single-file).

### 🧾 Migrazione dati
- Export/Import CSV per Clienti/Fornitori/Articoli.
- “Pacchetto migrazione” multi-CSV con import ordinato e controlli su DB non vuoto.

### ⚡ Fatturazione elettronica (feature flag)
- Aggiunta impostazione “Abilita fatturazione elettronica”.
- Modalità “Commercialista” (salvataggio XML in cartella) o “Server” (invio HTTP POST a endpoint esterno).

### 🧪 DEMO e UI
- Aggiunti dati DEMO per Assistenza/Contratti (seed idempotente) e purge aggiornato.
- Aggiunto campo Note in scheda Cliente.
- Migliorata leggibilità campi login (utente/password) e finestre cambio/reset password.

### 🧹 Build
- Build/publish Release puliti su Windows (warning NETSDK1206 silenziato nel progetto dati).

## [1.0.1] - 04/01/2026

### ✅ Correzioni e Affidabilità
- Risolti errori in apertura di alcune sezioni causati da eventi UI durante l'inizializzazione
- Migliorata la robustezza in apertura/refresh liste (null-check e guard di stato UI)

### 🖱️ Usabilità
- Standardizzato "doppio click sulla riga" per aprire/modificare nelle principali liste (equivalente a "Modifica"/"Apri")

### 🧪 Dati DEMO
- Aggiunti agenti DEMO e assegnazione best-effort ai clienti DEMO

### 💶 Formati
- Impostata cultura applicazione `it-IT` per formati coerenti (valuta in `€`, date e decimali)

## [1.0.0] - 31/12/2025

### 🎉 Release Iniziale

#### ✨ Nuove Funzionalità

**Modulo Magazzino**
- ✅ Gestione completa articoli con codici EAN, taglie, colori
- ✅ Supporto fino a 200 magazzini
- ✅ Gestione fino a 10.000 listini prezzi personalizzati
- ✅ Tracciamento giacenze in tempo reale
- ✅ Gestione numeri di serie e lotti
- ✅ Sistema di codici a barre integrato
- ✅ Alert articoli sottoscorta

**Modulo Fatturazione**
- ✅ Emissione preventivi
- ✅ Gestione ordini clienti e fornitori
- ✅ Emissione DDT
- ✅ Fatture accompagnatorie e differite
- ✅ Conversione automatica documenti (preventivo→ordine→DDT→fattura)
- ✅ Calcolo automatico sconti multipli
- ✅ Gestione IVA e reverse charge

**Modulo Fatturazione Elettronica**
- ⚠️ Export XML **minimale** (non garantito conforme FatturaPA)
- ⚠️ Firma digitale / invio SDI / ricezione / conservazione: **non presenti**
- ✅ Supporto B2B, PA e B2C

**Modulo Contabilità**
- ✅ Piano dei conti multilivello (2-8 livelli)
- ✅ Prima nota in partita doppia
- ✅ Registri IVA (vendite, acquisti, corrispettivi)
- ✅ Mastrini clienti e fornitori
- ✅ Gestione IVA detraibile/indetraibile
- ✅ Riepilogo IVA periodico

**Modulo Assistenze e Contratti**
- ✅ Schede assistenza/riparazioni
- ✅ Gestione contratti a tempo e monte ore
- ✅ Gestione cantieri con costi e ricavi
- ✅ Tracciamento interventi giornalieri
- ✅ Report margini per cantiere

**Modulo Archiviazione Documentale**
- ✅ Archiviazione file illimitata
- ✅ Acquisizione da scanner
- ✅ Sistema protocollo integrato
- ✅ Gestione RNC e azioni correttive
- ✅ Valutazione fornitori

**Infrastruttura**
- ✅ Architettura a 3 livelli (UI, Business, Data)
- ✅ Supporto SQLite e SQL Server
- ✅ Material Design UI moderna
- ✅ Sistema di logging integrato
- ✅ Backup automatico database

#### 🛠️ Miglioramenti Tecnici
- Entity Framework Core 7.x
- WPF con Material Design
- Dependency Injection
- Pattern Repository
- Gestione transazioni

#### 📝 Documentazione
- ✅ README completo
- ✅ Guida installazione
- ✅ Changelog
- ✅ Commenti XML nel codice
- ✅ File di configurazione documentato

#### 🐛 Bug Corretti
- N/A (Prima release)

#### ⚠️ Problemi Noti
- Fatturazione elettronica: richiede configurazione certificato digitale
- Report personalizzati: editor in fase di completamento
- App mobile: prevista in versione futura

---

## [Prossime Versioni]

### Pianificate per v1.1
- [ ] Completamento moduli interfaccia utente
- [ ] Report editor visuale
- [ ] Dashboard con grafici statistiche
- [ ] Export dati Excel/PDF
- [ ] Import massivo articoli da CSV
- [ ] Integrazione E-commerce
- [ ] API REST

### Pianificate per v2.0
- [ ] Interfaccia web responsive
- [ ] App mobile per agenti
- [ ] Business Intelligence avanzata
- [ ] Modulo CRM
- [ ] Gestione produzione
- [ ] Multi-azienda

---

## Note di Migrazione

### Da versione precedente a 1.0
N/A - Prima release

---

## Supporto Versioni

- **Versione 1.0**: Supportata fino a 31/12/2026
- **Aggiornamenti sicurezza**: Rilasciati regolarmente
- **Aggiornamenti funzionali**: Rilasciati trimestralmente

---

Per segnalare bug o richiedere funzionalità: support@didogest.com
