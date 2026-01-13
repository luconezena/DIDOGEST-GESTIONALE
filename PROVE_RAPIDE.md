# DIDO-GEST — Prove rapide (senza installazione)

Queste prove servono per verificare che l’app si apra e che i moduli principali siano operativi (niente popup “in sviluppo”).

## Avvio (portable)
- Percorso eseguibile (distribuzione): `portable\\dist\\DidoGest-Portable\\DidoGest.exe`
- In alternativa (comodità): `portable\\DidoGest.exe`
- Avvio: doppio click su `DidoGest.exe`.

Nota pratica (portable): tieni la cartella dell'app in un percorso scrivibile e locale (es. `Documenti` o `Desktop`).
Evita `Program Files`, cartelle di rete o cartelle sincronizzate (es. OneDrive) se possibile: possono bloccare o rendere “sola lettura” il database.

## Database (dati locali)
- Il database locale è `DidoGest.db` nella stessa cartella dell’eseguibile.
- Per provare “da zero”:
  1. Chiudi l’app
  2. Rinomina `DidoGest.db` in `DidoGest.db.bak`
  3. Riapri l’app (verrà ricreato un DB nuovo)

Se all'avvio compare un messaggio su DB non scrivibile / sola lettura / bloccato:
- Sposta la cartella dell'app (o il DB) in `Documenti/Desktop` e riprova.
- Verifica che `DidoGest.db` non abbia l'attributo “Sola lettura”.
- Controlla che non ci sia un'altra istanza aperta.

## Smoke test (10–15 minuti)
Esegui i seguenti click e verifica che si aprano schermate/finestre di inserimento/modifica.

Nota UX: nelle liste principali puoi anche usare **doppio click sulla riga** per aprire la finestra di modifica (equivalente al pulsante "Modifica"/"Apri").

### Anagrafiche
- Clienti: Nuovo → Salva → Modifica → Salva → Elimina (se presente)
- Fornitori: Nuovo → Salva → Modifica → Salva → Elimina
- Agenti: Nuovo → Salva

### Magazzino
- Articoli: Nuovo → Salva; Modifica → Salva (verifica prezzi e fornitore)
- Magazzini: Nuovo → Salva
- Movimenti: Nuovo → Salva (se previsto)
- Giacenze: verifica che la vista si apra e carichi

### Documenti
- DDT: Nuovo DDT → Salva
- DDT: Converti in Fattura (se disponibile) → verifica creazione fattura base
- Fatture: Nuova Fattura → Salva; Visualizza/Modifica → Salva

### Incassi / Scadenze
- Fatture: imposta `Pagato` e `Data Pagamento` → Salva → riapri e verifica che i valori siano rimasti
- Scadenzario Incassi: verifica filtri e ordinamenti (Pagate/Non pagate)
- Stampa: prova “Incassi periodo” (o stampa equivalente) e verifica che includa solo fatture con `Data Pagamento` nel periodo
- Solleciti: genera una stampa sollecito per una fattura non pagata con scadenza in passato

### Formati
- Verifica che gli importi siano mostrati con simbolo `€` e separatori italiani (es. `1.234,56 €`).

### Numerazioni
- Crea 2 fatture consecutive e verifica che la numerazione sia coerente (niente duplicati/numero mancante)
- Riapri una fattura già salvata e salva di nuovo: non devono esserci errori di numerazione

### Contabilità
- Prima Nota: Nuova Registrazione → Salva
- Mastrini: verifica apertura e caricamento

### Assistenza / Operativo
- Schede Assistenza: Nuovo/Modifica → Salva
- Contratti: Nuovo/Modifica → Salva
- Cantieri: Nuovo/Modifica → Salva

### Archivio
- Archivio Documenti: Nuovo Documento → seleziona un file → Salva

## Backup (verifica operativa)
- Menu: **Utility → Backup database**
- Verifica che venga creato un file in `Backup\\` accanto all'eseguibile
- (Opzionale) Ripeti più backup e verifica che la cartella non cresca senza limiti (l'app mantiene gli ultimi 30)

## Help (verifica operativa)
- Menu: **AIUTO → Guida del programma**
- Verifica che la guida si apra e che il testo sia presente (carica `HELP.md` dalla cartella dell'exe)

## Cosa segnare se qualcosa non va
- Quale menu/pulsante hai cliccato
- Se appare un messaggio e il testo esatto
- Se la finestra si apre ma non salva (errore/validazione)

## Log (se presenti)
- Controlla la cartella `Logs\\` accanto all'eseguibile se ci sono file utili per capire gli errori.
- In caso di errori UI: `ui-errors-YYYYMMDD.log`
- In caso di crash non gestito: `crash-YYYYMMDD.log`
