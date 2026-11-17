---
schema_version: 1
kind: story
lang: it
page_id: dashboard/US-DASH-02
source: docs/areas/dashboard/user-stories/US-DASH-02.md
source_hash: 98b3dacde579be6e4a21ae5d71fc7ca402e2b4ea
steps_from: DASH-RECENT-001
steps_hash: 2b1f9fea83c4a488749680b47649275e14b583af
routes: [/dashboard]
contexts: []
screenshots: [images/dashboard-default.png, images/dashboard-no-tasks.png, images/dashboard-recent-tasks-unavailable.png]
---
# Vedere le ultime attività

Guarda nella dashboard le attività create più di recente, per capire cosa richiede attenzione senza aprire l'elenco completo.

## Passaggi

1. Nel menu, fai clic su "Dashboard".
2. Leggi la sezione "Attività Recenti" sotto i quattro riquadri.

   ![La tabella Attività Recenti sotto i conteggi delle attività](images/dashboard-default.png)

## Cosa vedrai

- Una tabella con le colonne "Titolo", "Stato" e "Scadenza".
- Lo stato come etichetta colorata: "Da fare", "In corso", "Completata" o "Annullata".
- Un'attività senza scadenza mostra "-".
- Al massimo cinque attività, dalla più recente.
- Quando non ci sono attività, al posto della tabella gli Admin e gli Editor leggono "Nessuna attività. Vai ad Attività per crearne una." e i Visualizzatori leggono "Nessuna attività".

![La sezione Attività Recenti di un'organizzazione senza attività](images/dashboard-no-tasks.png)

## Da sapere

- Le cinque attività sono le più recenti tra tutte quelle dell'organizzazione, qualunque sia il loro stato.
- Non si può scegliere quante attività mostrare e non si può modificare un'attività dalla dashboard.

## Se qualcosa non va

| Cosa vedi | Cosa fare |
|---|---|
| "Il servizio non ha risposto. Riprova tra qualche minuto." | Ricarica la pagina più tardi. |

![La sezione Attività Recenti con il messaggio che il servizio non ha risposto](images/dashboard-recent-tasks-unavailable.png)

## Pagine correlate

- [Vedere i conteggi delle attività](US-DASH-01.md)
- [Aprire l'elenco delle attività dalla dashboard](US-DASH-03.md)
- [Leggere il saluto nella dashboard](US-DASH-04.md)
