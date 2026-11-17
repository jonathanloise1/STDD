---
schema_version: 1
kind: story
lang: it
page_id: authentication/US-AUTH-06
source: docs/areas/authentication/user-stories/US-AUTH-06.md
source_hash: df80286bb3b4930d14d24fbee6107c4cff9831a3
steps_from: AUTH-NAV-001
steps_hash: f62ee830eb04dbc1e1037cbf468fc74989c21798
routes: [/dashboard]
contexts: []
screenshots: [images/sidebar-menu-as-admin.png, images/not-found-default.png]
---

# Spostarsi tra le sezioni dell'app

Usa il menu a sinistra per raggiungere le sezioni dell'app. L'intestazione mostra l'organizzazione in cui stai lavorando.

## Passaggi

1. Accedi. Arrivi nella pagina "Dashboard".
2. Guarda il menu a sinistra. Elenca "Dashboard" e "Attività"; gli amministratori vedono anche "Team" e "Registro di audit", e "Impostazioni" in fondo.

   ![Il menu a sinistra della pagina Dashboard come lo vede un amministratore, con la voce Dashboard evidenziata](images/sidebar-menu-as-admin.png)

3. Clicca una voce per aprire la sua pagina. La voce della pagina aperta è evidenziata.
4. Se fai parte di più organizzazioni, clicca il nome di quella attuale nell'intestazione e scegline un'altra. Ogni pagina mostra poi i dati dell'organizzazione scelta.
5. Se finisci su un indirizzo che non esiste, clicca "Homepage" per tornare alla pagina "Dashboard".

   ![La pagina Pagina non trovata con il pulsante Homepage](images/not-found-default.png)

## Cosa vedrai

- L'intestazione di ogni pagina mostra il nome dell'organizzazione attuale.
- Solo la voce della pagina aperta è evidenziata.

## Da sapere

- Non sono disponibili la ricerca nell'app dall'intestazione né la modifica dell'ordine delle voci del menu.

## Pagine correlate

- [Aprire solo le pagine consentite dal tuo ruolo](US-AUTH-07.md)
- [Spostarsi tra le pagine con la tastiera](US-AUTH-09.md)
