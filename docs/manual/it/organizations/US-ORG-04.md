---
schema_version: 1
kind: story
lang: it
page_id: organizations/US-ORG-04
source: docs/areas/organizations/user-stories/US-ORG-04.md
source_hash: 7080bb6f1bec9cc6ba5086b3ce948b6fc20d84e8
steps_from: ORG-TEAM-001
steps_hash: 6f2009da8b3e181315ed82a22890f15308af3722
routes: [/settings/team]
contexts: []
screenshots: [images/team-default.png, images/team-list-unavailable.png]
---
# Vedere i membri del team

Guarda chi fa parte della tua organizzazione e con quale ruolo, per sapere chi può fare cosa.

## Prima di iniziare

- Solo gli Admin possono farlo. Gli Editor e i Visualizzatori non vedono "Team"; se aprono la pagina dal suo indirizzo, vengono portati alla Dashboard con il messaggio "Non hai accesso a questa pagina."

## Passaggi

1. Nel menu, fai clic su "Team".

   ![La pagina Team con la tabella dei membri](images/team-default.png)
2. Per trovare qualcuno, scrivi una parte del nome o dell'email in "Cerca membri del team...".

## Cosa vedrai

- La pagina "Gestione Team" seguita dal nome dell'organizzazione.
- La tabella "Membri" con nome, email e ruolo ("Admin", "Editor" o "Visualizzatore") di ogni membro.
- Sotto "Inviti in Attesa", le persone invitate che non hanno ancora effettuato l'accesso, con nome, email e data di invio.
- Quando nulla corrisponde alla ricerca, la tabella dice "Nessun membro del team corrisponde alla ricerca".
- La freccia indietro apre la pagina Organizzazione.

## Da sapere

- La ricerca non distingue tra maiuscole e minuscole.
- "Membri" elenca solo le persone che hanno già effettuato l'accesso; le persone invitate compaiono solo sotto "Inviti in Attesa".
- Non si può ordinare la tabella, dividerla in pagine, né inviare di nuovo o annullare un invito.

## Se qualcosa non va

| Cosa vedi | Cosa fare |
|---|---|
| "Il servizio non ha risposto. Riprova tra qualche minuto." | Ricarica la pagina più tardi. |

![La pagina Team con il messaggio che il servizio non ha risposto](images/team-list-unavailable.png)

## Pagine correlate

- [Vedere i dati dell'organizzazione](US-ORG-01.md)
- [Modificare i dati dell'organizzazione](US-ORG-02.md)
- [Caricare il logo dell'organizzazione](US-ORG-03.md)
- [Invitare un membro nel team](US-ORG-05.md)
- [Cambiare il ruolo di un membro](US-ORG-06.md)
- [Rimuovere un membro dal team](US-ORG-07.md)
