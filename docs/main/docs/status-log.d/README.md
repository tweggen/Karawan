# Status-log fragments

> **Status:** living process — KAR-462, 2026-10-19.

Entries for `../STATUS-LOG.md` that have landed but are not yet rolled up.
**Read these as well as STATUS-LOG.md**: they are the newest history.

## Why

Two agents often work in parallel. When every PR prepended its entry to the
top of `STATUS-LOG.md`, each merge put every other open PR into conflict
(5 of 5 merges on 2026-10-19). One file per entry cannot conflict.

## Writing an entry

- **A PR never edits `STATUS-LOG.md`.** It adds one file here instead.
- Name: `YYYY-MM-DD-<TICKET>-<slug>.md`, e.g.
  `2026-10-19-KAR-446-advance-npc-deterministic.md`. The date is the day the
  work lands; without a ticket use `NOTICKET` in its place.
- Content: exactly the entry as it would have stood in STATUS-LOG.md — one
  top-level bullet starting with `- ` (sub-bullets indented below it), e.g.
  `- ✅ **What changed (KAR-446, 2026-10-19)**: …`. No heading, no front matter.
- The one-line rule an entry teaches still goes into `CLAUDE.md` in the same PR.

## Rolling up

Occasionally (weekly, or before a release), one agent or the owner opens a
roll-up PR that contains nothing else:

```bash
python3 scripts/rollup-status-log.py --dry-run   # list what would be folded
python3 scripts/rollup-status-log.py             # fold and delete fragments
```

The script inserts the fragments at the top of the first section of
`STATUS-LOG.md`, newest first (date, then ticket number), and deletes them.
A fragment added by a PR still open at roll-up time is simply folded next time.

Thinktank should import this folder as well as STATUS-LOG.md (CYB-406, open);
until it does, a fragment reaches thinktank only after a roll-up.
