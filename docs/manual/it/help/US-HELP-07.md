---
schema_version: 1
kind: story
lang: it
page_id: help/US-HELP-07
source: docs/areas/help/user-stories/US-HELP-07.md
source_hash: cf2e7bb74ec63d20dbb21e78af8f92ad5f2d5841
steps_from: HELP-DOWNLOAD-001
steps_hash: 6b1024adc417f42c23d3627d83310efa72005696
routes: [/unanswered-questions]
contexts: []
screenshots: []
---

# Scaricare le domande senza risposta

Da amministratore, scarica le domande a cui il manuale non ha saputo rispondere, per capire cosa manca e farlo scrivere.

## Prima di iniziare

- Solo gli amministratori vedono la voce di menu "Domande senza risposta". Editor e Viewer no.

## Passaggi

1. Clicca "Domande senza risposta" nel menu. Si apre la pagina con lo stesso nome.
2. Scegli il primo giorno del periodo nel campo "Dal".
3. Scegli l'ultimo giorno del periodo nel campo "Al".
4. Clicca "Scarica". Ottieni un file con le domande del periodo che non hanno avuto risposta.

## Cosa vedrai

- Ogni voce mostra la domanda, la data e l'ora, la lingua, la schermata in cui è stata fatta e il motivo per cui non ha avuto risposta.
- Un periodo senza domande senza risposta dà un file senza voci.

## Da sapere

- Il motivo di ogni voce è uno tra "no_match" (il manuale non aveva nulla), "refused" (l'assistente ha rifiutato), "invalid" (la risposta non era utilizzabile), "not_helpful" (la persona ha premuto "No") o "timeout" (la risposta ha richiesto troppo tempo).
- Prima che una domanda venga conservata, gli indirizzi email sono sostituiti da "[email]", gli indirizzi web da "[url]" e i numeri di quattro o più cifre da "[number]". Una domanda viene conservata con al massimo 300 caratteri.
- Le voci non mostrano nessun nome di persona né organizzazione, quindi non si può sapere chi ha fatto la domanda.
- Non puoi leggere o modificare le domande a schermo, né attivare o disattivare la registrazione delle domande.

## Se qualcosa non va

| Cosa vedi | Cosa fare |
|---|---|
| "La data di fine non può precedere la data di inizio." | Il periodo finisce prima di iniziare. Correggi il campo "Al" in modo che non preceda il campo "Dal". |
| "Non è stato possibile scaricare le domande." | Riprova più tardi. |

## Pagine correlate

- [Fare una domanda sull'applicazione](US-HELP-04.md)
- [Dire se la risposta è stata utile](US-HELP-06.md)
