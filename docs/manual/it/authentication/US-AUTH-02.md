---
schema_version: 1
kind: story
lang: it
page_id: authentication/US-AUTH-02
source: docs/areas/authentication/user-stories/US-AUTH-02.md
source_hash: d49206fc7c548268288582d2d18981e0a308786c
steps_from: AUTH-LOGIN-002
steps_hash: aaead0cc145ff94637ff6dfa51246cea51cfc805
routes: [/dashboard]
contexts: []
screenshots: []
---

# Accedere prima di usare l'app

Se non hai ancora effettuato l'accesso, l'app te lo chiede prima di mostrarti qualsiasi cosa, così le informazioni della tua organizzazione restano private.

## Passaggi

1. Apri una pagina dell'app, per esempio quella delle attività, senza aver effettuato l'accesso. Si apre invece la pagina di accesso.
2. Accedi con il tuo indirizzo email e il codice che ricevi.
3. Si apre la pagina che avevi chiesto.

## Cosa vedrai

- Prima dell'accesso l'app non mostra menu, nome dell'organizzazione né dati.
- Dopo l'accesso si apre la pagina che avevi chiesto. Se avevi aperto l'indirizzo iniziale, vedi la pagina "Dashboard".

## Da sapere

- Ogni pagina dell'app richiede un utente che ha effettuato l'accesso. La pagina di accesso è l'unica che un visitatore può raggiungere.
- Se il tuo ruolo non consente la pagina richiesta, si apre la pagina "Dashboard" con il messaggio "Non hai accesso a questa pagina."
- Non esiste una pagina iniziale pubblica né un'anteprima dell'app, e l'app non spiega perché si è aperta la pagina di accesso.

## Pagine correlate

- [Accedere con il tuo indirizzo email](US-AUTH-01.md)
- [Aprire solo le pagine consentite dal tuo ruolo](US-AUTH-07.md)
