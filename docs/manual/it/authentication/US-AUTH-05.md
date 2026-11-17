---
schema_version: 1
kind: story
lang: it
page_id: authentication/US-AUTH-05
source: docs/areas/authentication/user-stories/US-AUTH-05.md
source_hash: a146e911e350f8f6dbe2acf6afe798c48e839b56
steps_from: AUTH-INVITE-001
steps_hash: 6a04473cd19dccf104fd4e9e7c5c582edd116155
routes: [/dashboard]
contexts: []
screenshots: []
---

# Entrare nell'organizzazione che ti ha invitato

Se qualcuno ti ha invitato nella sua organizzazione, ne entri a far parte accedendo. Non c'è nulla da accettare.

## Prima di iniziare

- Il tuo indirizzo email deve essere quello a cui è stato inviato l'invito.

## Passaggi

1. Apri nel browser l'indirizzo dell'app. Si apre la pagina di accesso.
2. Accedi con l'indirizzo email dell'invito e il codice che ricevi.

## Cosa vedrai

- Si apre la pagina "Dashboard" dell'organizzazione e l'intestazione ne mostra il nome.
- La pagina "Configura la Tua Organizzazione" non viene mostrata.
- Hai il ruolo indicato nell'invito. Un Editor vede il pulsante "Nuova Attività" nella pagina delle attività ma non la voce "Impostazioni" nel menu.

## Da sapere

- L'invito viene accettato dal tuo primo accesso con l'indirizzo email invitato, sia che tu sia nuovo sia che tu abbia già un account.
- L'invito lo invia un amministratore dell'organizzazione, che sceglie anche il tuo ruolo. Non è possibile rifiutare un invito.

## Pagine correlate

- [Accedere con il tuo indirizzo email](US-AUTH-01.md)
- [Iniziare a usare l'app al primo accesso](US-AUTH-04.md)
- [Aprire solo le pagine consentite dal tuo ruolo](US-AUTH-07.md)
