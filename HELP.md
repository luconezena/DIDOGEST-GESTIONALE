# Guida DIDO-GEST (Help)

Questa guida descrive il funzionamento operativo del gestionale **DIDO-GEST**.

## 1) Prima apertura (consigliato)

1. Apri **⚙️ Utility → Impostazioni**.
2. Controlla i percorsi principali:
   - **Percorso database** (SQLite) e/o **Provider database**.
   - **Percorso archivio** (cartella dove salvare allegati/archiviazione documentale).
3. Se lavori su più PC, valuta **SQL Server Express** e compila la stringa di connessione (sezione guidata).

## 2) Backup database

- Vai su **⚙️ Utility → Backup Database**.
- Il backup crea uno **snapshot consistente** del database SQLite.
- Vengono mantenuti gli ultimi backup (pulizia automatica).

Consiglio: fai un backup prima di importazioni massive o aggiornamenti.

## 3) Anagrafiche

### Clienti
- Menu: **👥 ANAGRAFICHE → Clienti**.
- Puoi creare/modificare clienti.
- Nella scheda cliente trovi anche **Note** (utile per annotazioni interne).

### Fornitori / Agenti
- Menu: **👥 ANAGRAFICHE → Fornitori** e **👥 ANAGRAFICHE → Agenti**.

## 4) Magazzino

- Menu: **📦 MAGAZZINO**.
- Funzioni principali:
  - **Articoli**: anagrafica e prezzi.
  - **Giacenze**: disponibilità per magazzino.
  - **Sottoscorta / Da riordinare**: controllo scorte minime.
  - **Movimenti**: storico carichi/scarichi.
  - **Listini Prezzi**: gestione listini.

## 5) Documenti

- Menu: **📄 DOCUMENTI**.
- Funzioni principali:
  - **DDT**
  - **Fatture**
  - **Fattura accompagnatoria**
  - **Preventivi**
  - **Ordini**

Suggerimento operativo: apri un documento in modifica con doppio click (dove previsto) e controlla righe, totali, IVA e intestazioni.

## 6) Fatturazione elettronica (integrazione esterna)

La sezione FE è **opzionale** e può essere attivata/disattivata.

### Abilitazione
1. Vai su **⚙️ Utility → Impostazioni**.
2. Spunta **Abilita fatturazione elettronica**.
3. Scegli la modalità:

### Modalità A: Commercialista (esporta XML)
- Imposta **Cartella XML (consegna al commercialista)**.
- Nel modulo FE, il comando **Genera/Invia XML** genera un file `.xml` nella cartella configurata.

### Modalità B: Server (invio a server esterno/API)
- Imposta **URL API** e (se necessario) **Chiave API / Token**.
- Il comando **Genera/Invia XML** invia l’XML via HTTP `POST` (content-type `application/xml`).
- Se il server richiede un formato diverso (JSON/multipart/header non standard), potrebbe essere necessario un adattamento.

Nota: l’XML generato è un **XML operativo minimale** (non sostituisce il tracciato ufficiale FatturaPA). Serve per integrare flussi esterni in modo pratico.

## 7) Assistenze

- Menu: **🔧 ASSISTENZE**.
- Funzioni principali:
  - **Schede Assistenza**: apertura, stato lavorazione, interventi.
  - **Contratti**: gestione contratti di assistenza/manutenzione.
  - **Cantieri**: gestione cantieri e interventi.

## 8) Archivio documentale

- Menu: **📂 ARCHIVIO → Archiviazione Documentale**.
- Usa l’archivio per conservare documenti e allegati collegati.

## 9) Import/Export dati (CSV) e pacchetto migrazione

- Menu: **⚙️ Utility → Importazione/Esportazione dati (CSV)**.
- Puoi:
  - Esportare/importare **Clienti**, **Fornitori**, **Articoli**.
  - Esportare/importare un **pacchetto migrazione** (cartella con più CSV in ordine numerato).

Importante: se il database non è vuoto, l’import chiede conferma per evitare sovrascritture accidentali.

## 10) DEMO (dati di esempio)

- In **⚙️ Utility → Impostazioni** puoi abilitare/disabilitare i dati DEMO.
- **⚙️ Utility → Pulisci dati DEMO** rimuove i record di esempio.

## 11) Risoluzione problemi (rapida)

- **Cartella non scrivibile / DB in sola lettura**: controlla permessi della cartella e attributi file.
- **DB bloccato (locked)**: chiudi eventuali copie del programma o tool che stanno usando il database.
- **Errore invio FE**: verifica URL/token e la compatibilità dell’endpoint.

---

Se vuoi un help più specifico per un flusso (es. “emettere fattura + generare XML + consegna commercialista”), dimmi il caso d’uso e lo aggiungo in questa guida.