# Login Errors Matched to the exli Backend — Plan

> **Status:** proposed; Fable-reviewed 2026-10-12; owner decided D1 (401 + JSON) and D2 (keep the server's voice); filed as **KAR-423** (agent-ready) · **Repos:** Karawan (client) and `../exli` (server) · **Written:** 2026-10-12
> **Builds on:** the login-screen feedback change (uncommitted in Karawan: `AutoSave.cs`, `ServerErrorText.cs`, `logos/Scene.cs`, `menu.xml`)
> **Thinktank:** component notes `login-menu` and `exli`; owner conventions for `login-menu` (a failed online login is reported with the server's message and never silently falls back to offline play; START starts the selected mode)

## Goal

When an online login or the initial online load fails, the login screen shows a reason that
1. quotes the server's own message wherever the server sends one, and
2. tells apart the conditions that need different reactions from the player: wrong credentials (fix them), a server that is unreachable, slow or broken (retry later or play offline), an expired or broken stored token (log in again, without the player doing anything).

The client already reports every failure (previous change). This plan makes the **server** say clearly what went wrong, and makes the **client** read exactly what this server sends, rather than guessing at generic error shapes.

## What the server does today

exli is a Phoenix app (Bandit) in `../exli/exli`; the game's `AutoSave.GameServer` is `https://silicondesert.io`. It runs **constantly on a dedicated server, on a Coolify instance in Dockerfile mode, deployed from git** (owner, 2026-10-12). It is no longer on Fly.io; the repo's `fly.toml` is stale. The `Dockerfile` builds a Phoenix release with `MIX_ENV=prod` and runs `bin/migrate && bin/server`, so **prod renders exceptions through `ErrorJSON`**, not the dev debug page (`start-server.sh` with plain `mix phx.server` is a dev leftover). Coolify puts a reverse proxy (Traefik by default) in front of the container; it answers by itself while exli is redeploying or down. The game calls three endpoints:

| # | Call | Condition | Server answer today | Client shows today (after the previous change) |
|---|---|---|---|---|
| 1 | `POST /api/public/users/log_in` | success | 200 JSON `{"token": "<url-safe base64, padded>"}` | — |
| 2 | same | wrong password **or** unknown email | **200**, no content type, body `I'm sorry, I do not recognize you.` | `login failed: I'm sorry, I do not recognize you. (200)` |
| 3 | same | body without `user.email`/`user.password`, or non-string values | 500 (FunctionClauseError / MatchError; `get_user_by_email_and_password` guards `is_binary`) | retried as transient (wrong: it is deterministic); the client never sends this |
| 4 | `GET /api/auth/save_game?gameTitle=…` | save exists | 200 JSON `{"save":{"gamedata","storedAt"}}` | — |
| 5 | same | no save yet | 404 text `Hi <email>, I'm afraid you didn't save any game yet.` | not an error: continues with the local game (**confirms** the client's 404 assumption) |
| 6 | same | `gameTitle` missing | **404** as in row 5. `games.name` is nullable and NULLs are distinct in the unique index, so `find_game(nil)` **inserts a new NULL-named `games` row on every call** | the client never sends this |
| 7 | any `/api/auth/*` | token unknown or older than 60 days | 403 text `I'm sorry, I don't remember you. Would you mind to login again, obtaining a fresh token?` (88 chars) | stored token: cleared, logs in again (good). Fresh token: `access refused: server answered 403 Forbidden` — the message is **dropped** because it exceeds the 64-char plain-text cap |
| 8 | any `/api/auth/*` | `x-nassau-token` header missing, duplicated, or not valid base64 | **500** — `ApiUserPlug` rescues only `RuntimeError`; the `MatchError` from `[token64] = …` / `{_, token} = Base.url_decode64(…)` escapes | `online save: Internal Server Error (500)`. **Why it sticks:** a 500 takes `_performApiCall`'s non-401/403 branch, which *stores* the token and sets logged-in before `onResponse`. The token is never cleared, so every later attempt fails the same way until the player edits eMail or password |
| 9 | `POST /api/auth/save_game` | insert fails | **200** `Hi <email>, I stored your exli.` — `add_save`'s result is ignored | cloud save believed successful |
| 10 | any | JSON body > 8 MB | 413 (Plug.Parsers default `length`) | cloud save: only uncategorised `Trace` lines, so it is invisible in a shipped build; `_setSaving(false)` runs and the player believes the save worked. Same silent-success class as row 9 |
| 11 | any | exli down, or the network drops | no answer (connection refused / timeout), or a proxy error page such as Traefik's `Bad Gateway` (502) | first login: `LoginTimeout` 15 s with 2 retries. **Returning player with a stored token:** `_performApiCall` with `ApiTimeout` 30 s and **no retry** on an exception |
| 12 | `GET /api/auth/save_game` | exli container not routed (Coolify redeploy or restart without a health check) | the **proxy's** 404 `404 page not found` (Traefik default) | **taken as "no save yet": a returning player (stored token, so no login call first) silently starts the local game** — against the owner rule. The login call would surface it (`login failed: 404 page not found (404)`), but a stored token skips the login |

Checked and found unreachable or already covered: `accepts ["json"]` → 406 (the .NET client sends no `Accept` header); CSRF (browser pipeline only); `Plug.Parsers` 400 on malformed JSON (the client never sends it); a reverse proxy's 502/503 while exli is down (HTML or short-text body → `Describe` fallback, retried as 5xx on login). There is no cold start: the server runs constantly.

Also found:
- `ApiUserPlug` writes **every session token to stdout** with `IO.inspect(token64)`, which ends up in the server's logs. `SaveGameController.create` inspects the game record.
- Exceptions render through `ErrorJSON` as `{"errors":{"detail":"<status text>"}}` in prod. Before the `:api` pipeline runs, a request without an `Accept` header gets `ErrorHTML`'s plain status text. Dev has `debug_errors: true` and renders debug HTML.
- Each API login inserts a `users_tokens` row that is never deleted; the 60-day expiry is the only cleanup. Not a bug, just noted.
- The API routes have no tests; only the generated browser auth tests exist.
- **Shipped clients today:** the old client calls `JsonSerializer.Deserialize` on the plain-text answer in row 2. That throws inside an unobserved continuation, so the old client **stalls forever on a wrong password**.

## Changes

### Server (exli)

S1–S4 edit only the three API modules (`ApiUserController`, `ApiUserPlug`, `SaveGameController`). `Accounts` and `UserAuth` are not touched, so the browser/LiveView auth flows are unaffected.

- **S1 — Login rejects with a status and JSON.** Row 2 becomes **401** `{"error": "I'm sorry, I do not recognize you."}` (the game's voice is kept). Guard the happy clause with `when is_binary(email) and is_binary(password)`. A catch-all `create/2` clause answers **400** `{"error": "expected user.email and user.password"}` (row 3).
- **S2 — The token plug fails as 403, always, quietly.** Every failure (missing, duplicated or non-base64 header, unknown or expired token) answers **403** `{"error": "I'm sorry, I don't remember you. Please log in again."}` and halts. Use `with` pattern matching rather than raise/rescue. Remove the `IO.inspect` calls that log tokens.
- **S3 — "No save yet" keeps 404 and gets JSON.** Row 5 becomes 404 `{"error": "no saved game yet"}`. The client depends on the 404 status, and the body no longer includes the player's eMail. Row 6 becomes **400** `{"error": "gameTitle is required"}`, checked **before** `find_game`, which stops inserting NULL-named `games` rows. Delete any existing NULL-named rows (and their `saves`, through `on_delete: :delete_all`) once, by hand, after checking the count. No migration: a `NOT NULL` constraint is a separate decision.
- **S4 — Store failures are reported.** In `create`, check `{:ok, _}`; on `{:error, changeset}` answer **500** `{"error": "could not store the save game"}`. Bad params answer **400**. Remove the `IO.inspect(game)`.
- **S5 — Body size, measured first.** Measure real save sizes: `select max(octet_length(gamedata)) from saves`. Raise `Plug.Parsers` `length:` only if they approach 8 MB. If they don't, record the measurement and leave the limit.
- **S6 — API tests.** Add `test/exli_web/controllers/api_test.exs` (ConnCase and fixtures exist): login ok / wrong password / unknown email / missing params / non-string params; save_game GET ok / 404 / missing title (no `games` row created); POST ok; plug with missing / duplicated / garbage / unknown token → 403.

### Client (Karawan)

- **C1 — Read exli's bodies exactly and set one real length cap.** `ServerErrorText` currently clips the server message to 64 chars, and callers then prepend `login failed: ` / `access refused: ` / `online save: ` *after* the clip, so there is no end-to-end cap. Change: `ServerErrorText` returns the full server message (plain text up to 200 chars; HTML still rejected by its leading `<`). Move the clip to one place, `Scene._displayLoginStatus`, sized to the 560 px status widget. The exact character count is set after a visual check (verification §3). Omit the status code for 2xx answers: today's row 2 shows `(200)`, which reads like success. Update `ClipsLongMessagesToOneLine` and add tests using the exact bodies from rows 2, 5, 7 and the new S1–S4 JSON bodies.
- **C2 — Document the 404 contract.** In `AutoSave._onLoadGameResponse`, add a comment that 404 means "no save yet" per exli `SaveGameController.index`. No behaviour change.
- **C6 — Only exli's own 404 means "no save yet"** (row 12). `_onLoadGameResponse` accepts a 404 as "no save" only if its body is exli's: the new S3 JSON `{"error": …}`, or, for the current server, the text containing `didn't save any game yet`. Any other 404 is an error: `online save: 404 page not found (404)`. Tests cover both exli bodies and Traefik's body.
- **C3 — Treat 400 as a client bug, not a login failure.** `_withWebToken` already does not retry 4xx. Use `login failed: …` for 401 and `server rejected the request: …` for other 4xx, so a malformed request doesn't read like bad credentials.
- **C4 — Make failed cloud saves visible** (rows 9 and 10). In `_onSaveGameResponse`, a non-2xx answer logs `Warning(_dc, …)` with the server's message. `AutoSave` gets a `Dc` category, and the `Trace` calls the previous change touched use it (CLAUDE.md's debug-output rule). No UI change.
- **C5 — Same retry for returning players.** Give the stored-token path (`_performApiCall`) the transient retry the login path already has (no answer or 5xx; `InitialLoginRetries` on the login screen). Then a brief network drop doesn't fail a returning player on the first try while a new player would get two retries. The timeouts stay as they are: the server runs constantly, so there is no cold start to wait for.

No code is needed to point a desktop build at a local exli: `engine/casette/Loader.cs` already applies `/implementations/<class>/properties` to public settable members. For the end-to-end check, a local (uncommitted) edit of `models/nogame.implementations.json`, `"nogame.modules.AutoSave": {"properties": {"GameServer": "http://localhost:4100"}}`, is enough.

## Compatibility

Every change works with both old and new counterparts, so client and server can ship in either order:

| | old server | new server |
|---|---|---|
| **old client (shipped Android)** | wrong password: stalls forever (today) | wrong password: JSON parses, token empty, 5 retries (~10 s), then the old client calls `onResponse` **and** `onError`: it falls back to the local game, and "error logging in." is immediately overwritten by "logged in." Better than a stall, but it still breaks the owner's rule — only a client update fixes that |
| **new client** | rows 2 and 7 quoted (C1); row 8 still sticks until the player edits credentials | all rows quoted, 403 clears a broken token automatically |

For old clients, S2 changes row 8 from 500 to 403: a stored garbage token is cleared and the login retried, an improvement. But a **fresh** token answered with 403 is only logged by the old client, which then stalls exactly as today (`HEAD:AutoSave.cs:227-230`). S2 isn't risk-free for old clients, but it makes nothing worse.

## Owner decisions

- **D1 — HTTP status for a rejected login:** 401 + JSON (recommended; it lets the client tell bad credentials from protocol errors), or keep 200 and only add a JSON body.
- **D2 — Message wording:** keep the server's voice ("I'm sorry, I do not recognize you."), or shorter neutral texts. The plan assumes the voice is kept.

## Verification

The local setup below (throwaway Postgres, exli test-port override, local exli with a seeded user) was built in **KAR-424**: see `docs/testing/docs/LOGIN_SERVER_HARNESS.md`.

1. **exli unit/controller tests:** `cd ../exli && ./test-local.sh`. Do not use `start-dockers.sh`: it binds 5432. The new API tests pass, and so do the existing auth tests and the KAR-424 smoke test.
2. **Client unit tests:** `ServerErrorTextTests` covers every row. The full xUnit suite must show no new failures against master. Master currently has 26 street-generation failures (measured 2026-10-12 in a clean worktree; see thinktank `testing`), while CLAUDE.md still claims a green 1804. Reconciling that is a separate item, not part of this plan.
3. **End to end:** run exli locally (`../exli/dev-local.sh`, port **4100**, seeded user) with the `GameServer` property override above; `Tooling/GameConfigSeed` presets a garbage token. With `debug_errors: true`, a local 500 renders as an HTML debug page, so the local run verifies the **post-S2** behaviour only (row 8 before S2 is known from the code). Check on the login screen: wrong password (quoted 401 message, no retry delay), unknown email (same), no save yet (local game starts), garbage token in the config (403 → token cleared → login succeeds), server stopped (`server not reachable` after the retries), a successful login, and whether the longest messages fit the 560 px status line (this sets C1's cap).
4. **Prod, after deploying exli:** a wrong-password login against `silicondesert.io` from the new client and from the shipped Android build.

## Docs to update on completion

- `docs/main/docs/persistence/` — the client/server contract table (rows above, after the change).
- `docs/main/docs/STATUS-LOG.md` — an entry for both changes. CLAUDE.md only if the current state changes.
- exli `README.md` — the API contract, the test command, and how production runs it (Coolify, Dockerfile mode, from git; `fly.toml` and `start-server.sh` are stale — delete them or mark them as such, owner's call). Configuring a Coolify health check (for example on `/`) would let Coolify keep the old container serving until the new one is up, which shrinks the row-12 window. Owner's call, outside the code.
- Thinktank: the `exli` component note revised to the new answers.
