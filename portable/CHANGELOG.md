# DIDO-GEST - Changelog

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
- ✅ Generazione XML formato FatturaPA
- ✅ Firma digitale documenti
- ✅ Invio tramite SDI (Codice: G4AI1U8)
- ✅ Ricezione fatture fornitori
- ✅ Conservazione sostitutiva 10 anni
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
- Entity Framework Core 8.0
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
