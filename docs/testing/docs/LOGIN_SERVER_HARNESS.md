# Login and Online Save Against a Local exli

How to test the game's login and online-save paths (`nogame.modules.AutoSave`)
against the exli backend on your own machine, without touching production or
any database you already run. Built in KAR-424; KAR-423 (matching the login
error answers) uses it for its acceptance checks.

exli lives in a sibling repository, `../exli` (Phoenix app in `../exli/exli`).
**Production is deployed by Coolify from exli's `main` branch: a push to `main`
ships. Work on a branch and merge through a PR.**

## What exists

| Where | What |
|-------|------|
| `../exli/test-local.sh` | Runs exli's whole test suite against a throwaway Postgres. |
| `../exli/dev-local.sh` | Runs exli on `http://localhost:4100` against a throwaway Postgres, with one seeded user. |
| `../exli/scripts/` | Their shared parts: the database container (`local-db.sh`) and the mix toolchain container (`toolchain.sh`, `Dockerfile.toolchain`). |
| `../exli/exli/priv/repo/seeds_local.exs` | Seeds the user; refuses to run outside dev, against a non-local database, or without `EXLI_LOCAL_SEED=1`. |
| `Tooling/GameConfigSeed/` (here) | Writes the game's stored login (`gameconfig.db`) into an RW path, optionally with a preset web token. |

Both exli scripts need only Docker. They:

- start Postgres 16 with `--rm`, no named volume, bound to `127.0.0.1:5433`
  (`EXLI_LOCAL_DB_PORT` picks another port). They never touch a Postgres
  that already runs on 5432, and they remove the container on every exit,
  including a failure or Ctrl-C;
- run mix in a container with the production Elixir 1.16.1 / OTP 26.2.2
  (Debian bookworm; bullseye's security packages have left
  `deb.debian.org`). It uses host networking, so it reaches the database on
  127.0.0.1 and serves 4100 on the host (Linux). Its deps, build output and
  Hex cache stay in `../exli/.local-cache/`, which is gitignored;
- read the database settings from env vars that `config/test.exs` and
  `config/dev.exs` now accept (`EXLI_TEST_DB_*`, `EXLI_DEV_DB_*`). Without
  them, both files keep their old values.

## exli's tests

```bash
cd ../exli
./test-local.sh                                    # whole suite
./test-local.sh test/exli_web/api                  # arguments go to mix test
```

`test/exli_web/api/game_api_smoke_test.exs` pins the game's path on a good
day: log in for a token, store a save, read it back.
`test/exli_web/controllers/api_test.exs` pins every error answer (KAR-423).

## The game against a local exli

1. **Start exli:** `cd ../exli && ./dev-local.sh`. It prints the seeded login,
   `player@local.test` / `local password` by default
   (`EXLI_LOCAL_USER_EMAIL`, `EXLI_LOCAL_USER_PASSWORD` change it). Ctrl-C
   stops it and discards the database.
2. **Point the game at it.** This is a local edit that is **never committed**.
   In `models/nogame.implementations.json`, change

   ```json
   "nogame.modules.AutoSave": null,
   ```
   to
   ```json
   "nogame.modules.AutoSave": { "properties": { "GameServer": "http://localhost:4100" } },
   ```

   The desktop build reads `models/` from the source tree, so no rebuild is
   needed. `engine/casette/Loader.cs` applies the properties. Undo it with
   `git checkout models/nogame.implementations.json`.
3. **Keep your own login and saves out of it** by giving the game its own RW
   path: `--rwpath <dir>` (or `JOYCE_RWPATH`).
4. **Run it**, either interactively:

   ```bash
   cd Karawan/bin/Debug/net10.0/linux-x64
   ./Karawan --rwpath /tmp/kar-local
   ```
   and log in on the login screen with the printed eMail and password;
   or unattended, with the login pre-seeded and auto-login on:
   ```bash
   dotnet run --project Tooling/GameConfigSeed -- /tmp/kar-local player@local.test "local password"
   ./Karawan --rwpath /tmp/kar-local --setting debug.option.autoLogin=global
   ```

On a fresh user, a successful run shows in exli's log as
`POST /api/public/users/log_in` → 200, `GET /api/auth/save_game` → 404
(`{"error": "no saved game yet"}`, so the game starts a new online game), and then
periodic `POST /api/auth/save_game` → 200.

### Login-screen cases to try

| Case | How |
|------|-----|
| Successful login | the seeded user |
| Wrong password / unknown eMail | type another password or eMail on the login screen |
| No save yet | any fresh user, the first login |
| Garbage web token | `GameConfigSeed <rwpath> <eMail> <password> 'not*a*token'`, then auto-login |
| Token the server no longer knows | log in once, then `docker exec $(docker ps -qf name=exli-local-db) psql -U postgres exli_local -c 'delete from users_tokens'` |
| Server unreachable | stop `dev-local.sh` (Ctrl-C) and log in |
| Long server messages | check that they fit the 560 px status line (OSD text does not wrap) |

What the server answers and what the game shows for each case is in
`docs/main/docs/persistence/GAME_SERVER_API.md` (since KAR-423: 401 for a
wrong login, 403 for any bad token, 404 `{"error": "no saved game yet"}`).

Two cases need a stand-in:

- **A 404 that is not exli's** (Traefik during a redeploy): point
  `GameServer` at `http://localhost:4101` and run
  `python3 -m http.server 4101 --bind 127.0.0.1` in an empty directory;
  seed a token so the game skips the login and asks for the save directly.
- **The old server:** `git stash -u` exli's changes (or check out the old
  commit) before `./dev-local.sh`; mix recompiles on start.

Locally, exli runs with `debug_errors: true`, so an unexpected 500 comes back
as an HTML debug page. Production renders it through `ErrorJSON`. A local run
therefore shows the client's fallback text for such a 500, not production's.

`dev-local.sh` stops on Ctrl-C. To stop it from a script, send SIGINT to its
child (`pkill -INT -P <pid of dev-local.sh>`): the script waits on the
`docker run` client, which forwards the signal, and its cleanup trap then
removes both containers. A SIGINT to the script alone waits until mix exits.

## Not covered

- No CI runs any of this; it is a local harness.
- Android and production are checked by hand after an exli deploy (step 4 of
  `LOGIN-SERVER-ERRORS.md` §Verification).
