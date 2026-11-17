---
schema_version: 1
kind: story
lang: it
page_id: tasks/US-TASK-01
source: docs/areas/tasks/user-stories/US-TASK-01.md
source_hash: 6962bf3bf2473f120bf647b367136284c23d43fe
steps_from: TASK-CREATE-001
steps_hash: 63a87f0b1479a65ff17efff3adfd9fd3b0413bd8
routes: [/tasks]
contexts: [task-form]
screenshots: [images/task-list-new-task-form-open.png]
---
# Crea un'attività

Usa questa pagina per aggiungere un'attività con un titolo. Il team può vedere cosa c'è da fare.

## Prima di iniziare

- Solo Admin ed Editor possono farlo.

## Passaggi

1. Apri la pagina Attività.
2. Fai clic su "Nuova attività".

   ![Il modulo Nuova attività sopra l'elenco delle attività](images/task-list-new-task-form-open.png)
3. Nel campo "Titolo" scrivi un valore come "Prepare the quarterly report".
4. Nel campo "Stato" scegli "Completata".
5. Fai clic su "Crea attività".

## Cosa vedrai

- Il modulo si chiude.
- La prima riga dell'elenco mostra la nuova attività.

## Da sapere

- Una nuova attività parte sempre con lo stato "Todo".
- Non puoi scegliere a chi è assegnata l'attività.

## Se qualcosa non va

| Cosa vedi | Cosa fare |
|---|---|
| "Il tuo ruolo non consente questa modifica." | Chiedi a un admin dell'organizzazione il ruolo di Editor. |
| "Inserisci un titolo." | Scrivi un titolo nel campo **Titolo**. |

## Pagine correlate

- [Modifica un'attività](US-TASK-02.md)
