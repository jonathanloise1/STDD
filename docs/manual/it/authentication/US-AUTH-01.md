---
schema_version: 1
kind: story
lang: it
page_id: authentication/US-AUTH-01
source: docs/areas/authentication/user-stories/US-AUTH-01.md
source_hash: d65099123badd36430656db598d29bf9d20f2ade
steps_from: AUTH-LOGIN-001
steps_hash: f27ec690c6a9d3d178a125050dfe9fcfbab9408d
routes: [/dashboard]
contexts: []
screenshots: []
---

# Accedere con il tuo indirizzo email

Accedi con il tuo indirizzo email e un codice che ricevi per posta. Non serve nessuna password.

## Passaggi

1. Apri nel browser l'indirizzo dell'app. Si apre la pagina di accesso.
2. Scrivi il tuo indirizzo email e conferma.
3. Apri la tua casella di posta, trova il messaggio con il codice e scrivilo nella pagina di accesso.
4. Guarda in alto a destra: il pulsante rotondo mostra le tue iniziali.

## Cosa vedrai

- Si apre la pagina "Dashboard".
- L'intestazione mostra il nome della tua organizzazione.
- Il pulsante in alto a destra mostra le tue iniziali.

## Da sapere

- L'indirizzo email e il codice sono l'unico modo per entrare. Non ci sono password e non si può accedere con un altro servizio, come un account social.
- La pagina di accesso, la durata del codice e il messaggio per un codice sbagliato sono forniti dal servizio di accesso, non dall'app.
- Resti connesso finché usi l'app e ricaricare la pagina non richiede un nuovo codice. Se la sessione non può più essere mantenuta, l'app ti chiede di accedere di nuovo.

## Se qualcosa non va

| Cosa vedi | Cosa fare |
|---|---|
| "Il servizio non ha risposto. Riprova tra qualche minuto." | Aspetta qualche minuto, poi ricarica la pagina. |

## Pagine correlate

- [Accedere prima di usare l'app](US-AUTH-02.md)
- [Uscire dall'app](US-AUTH-03.md)
- [Iniziare a usare l'app al primo accesso](US-AUTH-04.md)
- [Entrare nell'organizzazione che ti ha invitato](US-AUTH-05.md)
- [Spostarsi tra le sezioni dell'app](US-AUTH-06.md)
- [Aprire solo le pagine consentite dal tuo ruolo](US-AUTH-07.md)
- [Cambiare la lingua dell'app](US-AUTH-08.md)
- [Spostarsi tra le pagine con la tastiera](US-AUTH-09.md)
