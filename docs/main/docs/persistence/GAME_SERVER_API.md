# Game Server API — the Client/Server Contract

> **Status:** living reference · **Last updated:** 2026-10-24 (KAR-469)

What the game's online login and cloud save (`nogame/modules/AutoSave.cs`)
send to the exli backend, what exli answers, and what the player sees. The
server lives in the sibling repository `../exli` (Phoenix app in
`../exli/exli`, production `https://silicondesert.io`, deployed by Coolify
from exli's `main`). Its README carries the same table from the server's
side; change both together.

How to run all of this locally: `docs/testing/docs/LOGIN_SERVER_HARNESS.md`.

## Calls and answers

| Call | Condition | exli answers | The game does / shows |
|---|---|---|---|
| `POST /api/public/users/log_in` | success | 200 `{"token": …}` | calls the actual endpoint with the token, stores the token once that call is answered |
| same | wrong password or unknown eMail | **401** `{"error": "I'm sorry, I do not recognize you."}` | `login failed: I'm sorry, I do not recognize you. (401)` — no retry |
| same | parameters missing or not strings | **400** `{"error": "expected user.email and user.password"}` | `server rejected the request: … (400)` (the game never sends this) |
| `GET /api/auth/save_game?gameTitle=silicondesert2` | save exists | 200 `{"save": {"gamedata", "storedAt"}}` | loads the online save |
| same | no save yet | **404** `{"error": "no saved game yet"}` | continues with the local game state (or creates one) |
| same | a 404 from anything else (e.g. Traefik's `404 page not found` during a redeploy) | — | `online save: 404 page not found (404)`; never starts the local game |
| same | `gameTitle` missing | **400** `{"error": "gameTitle is required"}` | (the game never sends this) |
| `POST /api/auth/save_game` | stored | 200 text | — |
| same | not stored | **500** `{"error": "could not store the save game"}` | `Warning [Database] Cloud save failed: …` in the log, retried as transient first; no UI |
| same | parameters missing | **400** `{"error": "expected game.title and gamedata"}` | logged as above |
| any `/api/auth/*` | token missing, duplicated, not base64, unknown or older than 60 days | **403** `{"error": "I'm sorry, I don't remember you. Please log in again."}` | stored token: dropped, logs in again without the player doing anything. Fresh token: `access refused: …` |
| any | no answer, timeout, 5xx | — | retried (2 s apart): `InitialLoginRetries` (2) on the login screen, 5 for a background save — for a fresh login and a stored token alike. Then `server not reachable: …`, `the server did not answer in time`, or the server's message |

`nogame/modules/ServerErrorText.cs` turns an answer into text: it prefers
the `"error"` field (other common JSON shapes and short plain text are
accepted too, HTML is not), appends the status code except for a 2xx, and
`IsNoSaveYet` decides whether a 404 is exli's. The login screen clips the
whole status line once, in `Scene._displayLoginStatus`, to
`ServerErrorText.StatusLineLength` (69) characters: the 560 px status line
holds about 8 px per character in Prototype 17, and the longest line exli
causes is exactly 69 characters (550 px, measured with SkiaSharp).

## Connecting (IPv4 and IPv6)

`silicondesert.io` publishes an IPv4 (A) and an IPv6 (AAAA) address. On
desktop the game's `HttpClient` (registered in `Engine.cs`) races them
(`engine.net.HappyEyeballs`, RFC 8305): the first address — normally IPv6 —
gets 250 ms alone, then the next family starts beside it, and the first
connection wins. A bare `HttpClient` tries the addresses one after the other
and waits for each, so a published IPv6 address that does not answer used to
run the login into its 15 s `LoginTimeout` before IPv4 was tried, while
browsers (which race) kept working — the outage of 2026-10-24, when the host
lost its IPv6 after an upgrade. On Android the platform's native handler
stays (the .NET for Android default). Check the server with `curl -4` and
`curl -6` against `/api/public/users/log_in`: both must answer 401 for a
wrong password.

## Older servers and clients

Every combination works; client and server ship in either order.

- **New client, server before KAR-423:** a wrong password answered 200 with
  plain text; the client quotes it without the status (`login failed: I'm
  sorry, I do not recognize you.`). "No save" answered 404 with text
  containing `didn't save any game yet`, which `IsNoSaveYet` still accepts.
  A token that is not base64 answered 500, which the client then keeps until
  the player edits eMail or password. Verified end to end 2026-10-13.
- **Client before KAR-423 (shipped Android), new server:** a wrong password
  now answers 401 instead of 200 text. The old client deserialised the text
  and stalled forever; with 401 it retries (~10 s) and then falls back to the
  local game, with "error logging in." overwritten by "logged in.". Not
  verified with a build, read from the old code; only a client update gives
  the player the real message.

## History

- KAR-469 (2026-10-24): desktop connections race IPv6 and IPv4 (see
  Connecting); the server's lost IPv6 was restored with a static netplan
  override on the host.
- KAR-423 (2026-10-13): the statuses and JSON bodies above; before it, a
  wrong login answered 200, a bad token 500, and "no save" carried the
  player's eMail. Plan: `docs/main/plans/done/LOGIN-SERVER-ERRORS.md`.
