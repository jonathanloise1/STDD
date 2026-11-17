---
schema_version: 1
kind: story
lang: it
page_id: authentication/US-AUTH-07
source: docs/areas/authentication/user-stories/US-AUTH-07.md
source_hash: c599aa4f91bbfa084d79539f6fa0dc57f949dca2
steps_from: AUTH-ACCESS-001
steps_hash: 751643b6179f63292bb64cacd0b811f8cbecc22b
routes: [/dashboard]
contexts: []
screenshots: [images/sidebar-menu-as-admin.png, images/sidebar-menu-as-editor.png]
---

# Aprire solo le pagine consentite dal tuo ruolo

Le pagine di gestione sono aperte solo agli amministratori. Editor e Viewer vedono solo le pagine che possono usare.

## Prima di iniziare

- Gli amministratori vedono tutte le voci. Editor e Viewer non possono aprire le pagine di gestione.

## Passaggi

1. Da amministratore, guarda il menu a sinistra: elenca "Dashboard", "Attività", "Team", "Registro di audit" e "Impostazioni".

   ![Il menu di un amministratore con Dashboard, Attività, Team, Registro di audit e Impostazioni](images/sidebar-menu-as-admin.png)

2. Da Editor o Viewer, il menu elenca solo "Dashboard" e "Attività", e il menu dell'avatar offre solo "Esci".

   ![Il menu di un Editor con le sole voci Dashboard e Attività](images/sidebar-menu-as-editor.png)

3. Se apri una pagina riservata agli amministratori scrivendo il suo indirizzo, si apre la pagina "Dashboard" con il messaggio "Non hai accesso a questa pagina."

## Cosa vedrai

- Gli amministratori possono aprire le pagine Team, Registro di audit, Organizzazione e Modifica organizzazione.
- Amministratori, Editor e Viewer possono aprire le pagine "Dashboard" e "Attività".

## Da sapere

- Cosa puoi fare dipende dal tuo ruolo nell'organizzazione selezionata nell'intestazione.
- Il ruolo di un membro si cambia nella pagina del Team, da un amministratore.

## Pagine correlate

- [Accedere prima di usare l'app](US-AUTH-02.md)
- [Spostarsi tra le sezioni dell'app](US-AUTH-06.md)
