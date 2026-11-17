---
schema_version: 1
kind: story
lang: it
page_id: authentication/US-AUTH-09
source: docs/areas/authentication/user-stories/US-AUTH-09.md
source_hash: 70a72d2182224100fdd5b41a587bc27543826cbf
steps_from: AUTH-SHORTCUT-001
steps_hash: cf16399877e065f519fec2cc91a7535c01770022
routes: [/dashboard]
contexts: []
screenshots: [images/shortcuts-dialog-open.png, images/shortcuts-dialog-open-as-viewer.png]
---

# Spostarsi tra le pagine con la tastiera

Apri le pagine principali con pochi tasti, senza usare il mouse.

## Passaggi

1. Premi **G** e poi **D** per aprire la pagina "Dashboard", oppure **G** e poi **T** per aprire la pagina "Attività".
2. Da amministratore, premi **G** e poi **M** per la pagina del Team, **G** e poi **A** per il Registro di audit, oppure **G** e poi **S** per la pagina Organizzazione.
3. Premi **?** per aprire la finestra "Scorciatoie da tastiera". Elenca solo le scorciatoie che puoi usare.

   ![La finestra Scorciatoie da tastiera come la vede un amministratore, con le scorciatoie di navigazione](images/shortcuts-dialog-open.png)

4. Da Editor o Viewer, la finestra elenca solo le scorciatoie di Dashboard e Attività.

   ![La finestra Scorciatoie da tastiera come la vede un Viewer, con le sole voci Dashboard e Attività](images/shortcuts-dialog-open-as-viewer.png)

## Cosa vedrai

- Si apre la pagina della scorciatoia che hai premuto.

## Da sapere

- Editor e Viewer hanno solo le scorciatoie delle pagine Dashboard e Attività: per loro G seguito da M, A o S non fa nulla.
- Le scorciatoie non fanno nulla mentre scrivi in un campo o mentre è aperta una finestra: i tasti digitati in un campo sono testo.
- Non puoi scegliere altri tasti e non ci sono scorciatoie per le azioni dentro una pagina, come creare un'attività.

## Pagine correlate

- [Spostarsi tra le sezioni dell'app](US-AUTH-06.md)
- [Aprire solo le pagine consentite dal tuo ruolo](US-AUTH-07.md)
