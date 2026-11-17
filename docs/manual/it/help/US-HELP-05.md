---
schema_version: 1
kind: story
lang: it
page_id: help/US-HELP-05
source: docs/areas/help/user-stories/US-HELP-05.md
source_hash: 44d3b0f2bf945165c19bb6878aa079ddf555b2ac
steps_from: HELP-ANSWER-001
steps_hash: 032b8f40358047457943c07dc079021b895a1318
routes: [/manual]
contexts: []
screenshots: []
---

# Ricevere una risposta breve con la fonte

Quando l'assistente può rispondere, ricevi una risposta scritta breve che dice da dove viene, così puoi agire subito e controllare la fonte nel manuale.

## Passaggi

1. Apri la scheda "Chiedi" del pannello dell'aiuto, scrivi la domanda nel campo "La tua domanda" e clicca "Chiedi".
2. Leggi la scheda "Risposta dal manuale". Compare sopra le schede delle sezioni e si riempie man mano che viene scritta.
3. Clicca un link alla fonte, come **[1]**, per aprire la sezione citata. Le sezioni citate sono le prime schede.
4. Guarda la riga in fondo alla risposta: dice "Risposta generata su questo dispositivo" oppure "Risposta generata dal server".

## Cosa vedrai

- Ogni risposta mostrata ha almeno un link alla fonte; una risposta senza fonte non viene mai mostrata.
- Se l'assistente rifiuta o la sua risposta non è utilizzabile, il pannello mostra "L'assistente non ha potuto rispondere dal manuale. Queste sono le sezioni più vicine:" seguito dalle schede delle sezioni.
- Se la risposta richiede troppo tempo, il pannello mostra le schede delle sezioni con il titolo "Risultati migliori nel manuale".

## Da sapere

- Prima di un primo download grande, il pannello chiede "Scaricare l'assistente su questo dispositivo ({size} MB)? Le risposte verranno poi generate su questo dispositivo." con i pulsanti "Scarica" e "Non ora". Non viene scaricato nulla di grande senza la tua scelta, e "Non ora" viene ricordato. Accettare o no sta a te: senza il download ricevi comunque le schede delle sezioni.
- Durante il download il pannello mostra "Preparazione dell'assistente su questo dispositivo ({done} di {total} MB)" e nel frattempo risponde con le schede delle sezioni.
- Non puoi scegliere quale assistente risponde.

## Se qualcosa non va

| Cosa vedi | Cosa fare |
|---|---|
| "L'assistente non ha potuto rispondere dal manuale. Queste sono le sezioni più vicine:" | Apri una delle schede sotto il messaggio. |
| "Risultati migliori nel manuale" | La risposta ha richiesto troppo tempo. Apri una delle schede sotto. |

## Pagine correlate

- [Fare una domanda sull'applicazione](US-HELP-04.md)
- [Dire se la risposta è stata utile](US-HELP-06.md)
