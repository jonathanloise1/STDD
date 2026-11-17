---
schema_version: 1
kind: story
lang: it
page_id: authentication/US-AUTH-03
source: docs/areas/authentication/user-stories/US-AUTH-03.md
source_hash: 9880d9873bdf2571b31fad74ac8c1242c55845a1
steps_from: AUTH-LOGOUT-001
steps_hash: 0316395cb0cdbe257b2be6af6105428c8fd8ae7a
routes: [/dashboard]
contexts: []
screenshots: [images/header-menus-avatar-menu-open.png, images/header-menus-avatar-menu-open-as-editor.png]
---

# Uscire dall'app

Esci quando hai finito, così nessun altro può usare il tuo account su questo dispositivo.

## Passaggi

1. Clicca il pulsante rotondo con le tue iniziali in alto a destra, in qualsiasi pagina. Si apre un menu.

   ![Il menu dell'avatar aperto in alto a destra, con Impostazioni ed Esci](images/header-menus-avatar-menu-open.png)

2. Clicca "Esci". Se non sei un amministratore, il menu offre solo questa voce.

   ![Il menu dell'avatar di un Editor, con la sola voce Esci](images/header-menus-avatar-menu-open-as-editor.png)

3. Si apre la pagina di accesso.

## Cosa vedrai

- Si apre la pagina di accesso.
- Aprendo qualsiasi pagina dell'app ti viene chiesto di accedere di nuovo.

## Da sapere

- L'uscita termina la sessione solo su questo dispositivo: non ti disconnette dagli altri.
- Uscire non elimina il tuo account né i dati della tua organizzazione.
- L'app non chiede conferma prima di farti uscire.
- Gli amministratori trovano in questo menu anche "Impostazioni".

## Pagine correlate

- [Accedere con il tuo indirizzo email](US-AUTH-01.md)
- [Accedere prima di usare l'app](US-AUTH-02.md)
