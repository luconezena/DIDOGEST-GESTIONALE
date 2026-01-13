# DIDO-GEST — Requisiti e Copertura

Fonte requisiti: specifica interna (rimossa dal repository)

Legenda:
- **FATTO** = presente e usabile
- **PARZIALE** = presente ma incompleto / manca workflow
- **NON PRESENTE** = non trovato in modello/UI/workflow
- **DA VERIFICARE** = non ancora analizzato nel dettaglio

---

## Magazzino e fatturazione

- Dettagliate anagrafiche articoli — **FATTO**
- Taglie e colori a 1/2/3 livelli — **NON PRESENTE**
- Listini fino a 10.000 — **PARZIALE** (CRUD presente, mancano funzioni “massive”)
- Multi magazzini/negozi fino a 200 — **PARZIALE** (multi-magazzino e giacenze OK; max 200 non “imposto”)
- Preventivi e ordini (clienti/fornitori) — **PARZIALE** (workflow preventivo→ordine cliente OK; ordini fornitori da completare)
- DDT, fatture accompagnatorie e differite — **PARZIALE** (DDT/fattura OK; accompagnatoria non distinta; differita MVP via DDT→Fattura)
- Carichi da DDT e fatture fornitori — **FATTO (MVP)** (documenti con fornitore generano CARICO automatico; fattura derivata da DDT non ricarica)
- Articoli di servizio (no movimenti) — **PARZIALE** (flag presente; automatismi da verificare)
- Numeri serie / lotti / IMEI — **PARZIALE** (campi presenti; UI completa da fare)
- Barcode + stampa etichette personalizzate — **PARZIALE** (EAN sì; stampa etichette no)
- Multivaluta — **NON PRESENTE**
- Imposte RAE — **NON PRESENTE**
- Reverse charge — **PARZIALE** (campi presenti; UI/calcoli no)
- Disponibilità magazzino immediata — **FATTO**
- Prodotti sottoscorta / riordino — **PARZIALE** (scorta minima sì; vista riordino no)
- Conversioni documenti (prev→ord→DDT→fattura diff.) — **FATTO (MVP)** (preventivo→ordine→DDT→fattura; evita doppio scarico su fattura derivata da DDT)
- Stampe personalizzabili (report editor) — **NON PRESENTE**
- Gestione agenti — **PARZIALE** (anagrafica sì; integrazione nei documenti/statistiche da fare)
- Statistiche — **NON PRESENTE**
- Foto — **PARZIALE** (campo presente; UI upload/preview da fare)

Note: l’editor righe documento/ordine è minimale e serve come base.

---

## Fatturazione elettronica semplificata

- Abilitazione/disabilitazione modulo — **FATTO** (feature flag da impostazioni; menu FE “gated”)
- Emissione XML — **PARZIALE** (export XML “minimale”, non conforme al tracciato FatturaPA)
- Modalità “Commercialista” (salva XML in cartella) — **FATTO**
- Modalità “Server” (HTTP POST XML verso endpoint esterno) — **FATTO (base)**
- Firma digitale — **NON PRESENTE**
- Invio tramite SDI / hub (Codice SDI: G4AI1U8) — **NON PRESENTE** (solo flag “Inviato” impostabile manualmente)
- Ricezione fatture fornitori tramite SDI — **NON PRESENTE**
- Conservazione sostitutiva 10 anni — **NON PRESENTE**

Evidenze principali: [DidoGest.UI/Views/FatturazioneElettronica/FatturazioneElettronicaView.xaml.cs](DidoGest.UI/Views/FatturazioneElettronica/FatturazioneElettronicaView.xaml.cs)

---

## Contabilità base

- Piano dei conti multilivello 2–8 — **PARZIALE** (modello gerarchico presente; manca UI di gestione; livelli non vincolati)
- Causali multiple + automatismi contabilizzazione — **PARZIALE** (campo causale testuale; automatismi/tabelle causali non presenti)
- Prima nota in partita doppia — **PARZIALE** (registrazione con totali Dare/Avere; movimenti per conto non gestiti da UI)
- Ricerche/viste personalizzabili — **NON PRESENTE** (filtri base per data)
- Mastrini (clienti/fornitori/conti) — **PARZIALE** (vista mastrino presente; dipende da `MovimentiContabili` non inseribili da UI)
- Registri IVA (acquisti/vendite/corrispettivi) — **PARZIALE** (vista per tipo/periodo; stampa placeholder; mancano inserimento/generazione da documenti)
- IVA detraibile/indetraibile — **PARZIALE** (campi presenti nel modello; non gestiti in UI)
- IVA ad esigibilità differita — **PARZIALE** (campi presenti nel modello; non gestiti in UI)
- Riepilogo IVA — **NON PRESENTE**

Evidenze principali: [DidoGest.Core/Entities/PianoDeiConti.cs](DidoGest.Core/Entities/PianoDeiConti.cs), [DidoGest.UI/Views/Contabilita/PrimaNotaView.xaml.cs](DidoGest.UI/Views/Contabilita/PrimaNotaView.xaml.cs), [DidoGest.UI/Views/Contabilita/MastriniView.xaml.cs](DidoGest.UI/Views/Contabilita/MastriniView.xaml.cs), [DidoGest.UI/Views/Contabilita/RegistriIVAView.xaml.cs](DidoGest.UI/Views/Contabilita/RegistriIVAView.xaml.cs)

---

## Assistenze, cantieri e contratti

- Workflow assistenze (schede, verifiche, lavorazioni, ricambi, consegna) — **PARZIALE** (scheda base + stato; mancano “prime verifiche”, “lavorazioni/materiali”, collegamenti documenti e interventi dettagliati)
- Contratti (date, monte ore, fatturazione automatica) — **PARZIALE** (CRUD completo; manca fatturazione automatica da scadenze/contratti)
- Cantieri (date, operai, costi, margine) — **PARZIALE** (CRUD base con costi/ricavi; mancano giornate/interventi operai/squadre e materiali da magazzino)
- Stampa etichette/ricevute/distinte/DDT per assistenze — **NON PRESENTE**
- Numeri di serie anche via barcode + integrazione lotti/seriali — **NON PRESENTE**

Evidenze principali: [DidoGest.UI/Windows/SchedaAssistenzaEditWindow.xaml.cs](DidoGest.UI/Windows/SchedaAssistenzaEditWindow.xaml.cs), [DidoGest.UI/Windows/ContrattoEditWindow.xaml.cs](DidoGest.UI/Windows/ContrattoEditWindow.xaml.cs), [DidoGest.UI/Windows/CantiereEditWindow.xaml.cs](DidoGest.UI/Windows/CantiereEditWindow.xaml.cs)

---

## Archiviazione documentale

- Archiviazione documenti + catalogazione da cartella — **PARZIALE** (archiviazione presente ma salva percorso assoluto; non gestisce “cartella documenti unica”/copia file)
- Import file (pdf/doc/xls/zip/img/exe/…) — **PARZIALE** (selezione qualsiasi file; non copia nel repository documentale)
- Acquisizione da scanner (TWAIN) — **NON PRESENTE**
- Associazioni a clienti/fornitori/agenti/contatti e articoli — **PARZIALE** (campi nel modello; UI di associazione non presente)
- Parametri protocollo + ricerca — **PARZIALE** (protocollo auto + ricerca semplice; stato/apertura/chiusura non gestiti in UI)
- Stand-alone — **FATTO** (modulo utilizzabile da menu dedicato)
- Rete multi terminali/operatori + privilegi utenti — **NON PRESENTE**
- RNC/AC/valutazione fornitori + stampe — **NON PRESENTE**
- Messaggistica interna + ricevute consegna — **NON PRESENTE**
- Manuale qualità + accessi per mansione — **NON PRESENTE**

Evidenze principali: [DidoGest.UI/Views/Archivio/ArchivioView.xaml.cs](DidoGest.UI/Views/Archivio/ArchivioView.xaml.cs), [DidoGest.UI/Windows/DocumentoArchivioEditWindow.xaml.cs](DidoGest.UI/Windows/DocumentoArchivioEditWindow.xaml.cs), [DidoGest.Core/Entities/DocumentoArchivio.cs](DidoGest.Core/Entities/DocumentoArchivio.cs)
