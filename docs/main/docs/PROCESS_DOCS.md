# Documentation Organization Guide (PROCESS_DOCS)

This document defines the standard structure for Karawan documentation, so it
stays navigable as the project grows.

> **Status:** current — describes the domain-first layout adopted 2026-07-21.
> **Last Updated:** 2026-07-23

---

## The core idea: domain first, then purpose

Documentation is organized by **domain** (also called a module) first, and by
**purpose** second. A domain is a subsystem large enough to own its own corner
of the tree — `tale`, `navigation`, and so on. Inside each domain, every
document falls into one of three purposes: a plan, living reference, or history.

```
docs/
├── <domain>/
│   ├── plans/
│   │   ├── todo/        # agreed work, not yet started
│   │   ├── proposed/    # ideas, not yet committed to
│   │   └── done/        # plans that were executed
│   ├── docs/            # living reference: architecture, test specs, concepts, design
│   └── archive/         # history: completion reports, one-off investigations, superseded plans
│
└── main/                # the catch-all domain (see below)
```

`docs/` may nest one more level for grouping inside a bucket when a domain is
large — e.g. `tale/docs/phases/`, `tale/docs/tests/`, `main/docs/architecture/`.
Keep it shallow: `docs/<domain>/<bucket>/<category>/file.md` is the deepest a
document should sit.

---

## Current domains

A label earns its own top-level domain only if it carries **≥ 3 documents and
≥ 1 plan**. Anything below that bar folds into `main` rather than becoming a
folder with one lonely file in it.

| Domain | Docs | What it covers |
|---|---:|---|
| `tale` | 55 | The TALE narrative system: storylets, DES simulation, population, conversations, social/factions. Absorbs narration and TALE test specs. |
| `main` | 12 | Catch-all for below-bar subsystems: rendering, animation, engine core, build, persistence, platforms, quest, and this doc-process guide. |
| `navigation` | 11 | Routing, pathfinding, navmesh, traffic, pipes/flow — merged near-synonyms. |
| `testing` | 7 | Test infrastructure that is not TALE-content-specific: the ExpectEngine harness, tier model, testbed, runner guides. |
| `debug-infra` | 4 | The `DebugFilter`/`Dc` category logging system and its migration. The one domain with genuinely in-flight work. |
| `world-gen` | 3 | Procedural world generation: L-system engine and editor, fragment operators. |

`main` is deliberately not "miscellaneous". It is where a subsystem lives *until
it clears the bar*. When a fifth rendering doc and its first real plan appear,
`rendering` graduates out of `main` into its own domain — the promotion is
cheap because everything already lives under `main/docs/rendering/` etc.

Process files sit where their domain does:

| File | Location |
|---|---|
| `PROCESS.md` | repo root (generic, applies to everything) |
| `PROCESS_TALE.md` | `docs/tale/docs/` |
| `PROCESS_DOCS.md` | `docs/main/docs/` (this file) |

---

## Decision tree: where does a new document go?

```
1. Which domain is it about?
   Match it to a domain in the table above. If nothing fits, and you can already
   see ≥ 3 docs + ≥ 1 plan coming, propose a new domain. Otherwise → main.

2. Which purpose is it?
   Does it propose work to do?
     ├─ YES → is the work…
     │        agreed and about to start?   → <domain>/plans/todo/
     │        just an idea / not committed? → <domain>/plans/proposed/
     │        already executed?             → <domain>/plans/done/
     └─ NO

   Is it living reference someone will maintain and keep reading?
     (architecture, a test spec, a concept, a design doc)
     ├─ YES → <domain>/docs/   (add a category subfolder if the domain is large)
     └─ NO

   Is it history — a completion report, a one-off investigation, a superseded plan?
     └─ YES → <domain>/archive/
```

The distinction that trips people up is **plans/done vs. archive**. A plan that
was executed goes to `plans/done` — it is still a plan, just a finished one.
A *completion report* about that work, or a dated investigation, goes to
`archive` — it was never a plan, it is a record. Keeping them apart stops
`plans/done` from silting up into a junk drawer.

---

## Naming conventions

File names are uppercase. Two word-separators are in use by convention:

- **`SNAKE_CASE`** for phase docs, test specs, and reference material:
  `PHASE_0.md`, `PHASE_D_SOCIAL.md`, `REFERENCE.md`, `TESTING_STRATEGY.md`
- **`KEBAB-CASE`** for named plans:
  `MULTI-OBJECTIVE-ROUTING.md`, `PHASE-C1-INFRASTRUCTURE.md`, `TALE-SOCIAL-PHASE-E.md`

Prefer a short, descriptive name over a long prefixed one. `MULTI-OBJECTIVE-ROUTING.md`
inside `navigation/docs/` beats `IMPLEMENTATION-PLAN-PHASE-D-ROUTING.md` — the
domain and bucket already carry the context the prefix used to.

Top-level process files are the exception: uppercase, no separator style enforced
(`PROCESS.md`, `PROCESS_TALE.md`, `PROCESS_DOCS.md`).

---

## Document conventions

### Every document should have

1. **Title** — a single H1: `# TALE Phase 5: Escalation System`
2. **Status line** — for any doc describing work in progress:
   ```markdown
   > **Status:** implemented | partially-implemented | superseded | proposed | not-started
   > **Last Updated:** 2026-07-23
   ```
   Plans additionally carry a machine-checked header (see below).
3. **Purpose statement** — one or two sentences on what the document is for.
4. **Clear sections** — consistent H2/H3 hierarchy.
5. **Cross-references** — relative links to related docs.

### Verified status headers (plans)

Plans carry a header stating what the codebase actually shows, distinct from
whatever the author last claimed:

```markdown
> **Status:** implemented - **Implemented as:** `path/to/File.cs:NN` - **Verified:** 2026-07-23
```

When a plan's own status line and this header disagree, that is a signal the
plan needs a rewrite, not that the header is wrong — the header is the evidence.
Such contradictions are tracked in `docs/_migration/REVIEW.md` under "suspicious".

### Phase docs, test specs, design docs

- **Phase** (`tale/docs/phases/PHASE_N.md`): goals, systems/classes affected,
  a pointer to its test spec, known limitations.
- **Test spec** (`tale/docs/tests/PHASE_N.md`): test count and organization,
  categories, expected outcomes, links to the actual tests under
  `models/tests/tale/phaseN-*/`.
- **Design** (`<domain>/docs/design/THING.md`): problem, approach, trade-offs,
  and which phase or system implements it.

---

## Pending placement decisions

Four documents were deliberately left in their old locations pending a
todo-vs-proposed judgment call, rather than being guessed at:

| Document | Old location | Open question |
|---|---|---|
| `BUILD-ASSET-DEPENDENCY-TRACKING.md` | `docs/roadmap/proposed/` | `main/plans/todo` or `proposed`? |
| `TRAFFIC-LIGHTS-SYSTEM.md` | `docs/roadmap/proposed/` | `navigation/plans/todo` or `proposed`? |
| `IMPLEMENTATION-PLAN-PHASE-D-ROUTING.md` | `docs/roadmap/proposed/` | `navigation/plans/done` or `todo`? |
| `SYSTEMS/README.md` | `docs/SYSTEMS/` | keep as an index, or drop? |

See `docs/_migration/REVIEW.md` for the full framing of each. When resolved,
the empty `docs/roadmap/` and `docs/SYSTEMS/` trees can be removed.

---

## Quarterly review

Run a doc-structure audit each quarter:

- [ ] No documents in `docs/` root or `docs/roadmap/` that should be in a domain
- [ ] Domain bar still holds — has anything in `main` grown past ≥ 3 docs + ≥ 1 plan?
- [ ] `plans/done` holds executed plans, not completion reports (those belong in `archive`)
- [ ] Cross-references resolve (no links to files that moved or never existed)
- [ ] Status lines and verified headers agree, or the disagreement is tracked
- [ ] Naming conventions followed (uppercase; SNAKE for reference, KEBAB for plans)

Commit: `docs: quarterly structure audit [date]`

---

## Anti-patterns to avoid

❌ **Don't:**
- Put a completion report or investigation in `plans/done` — that is `archive`.
- Spin up a domain for one or two documents — keep them in `main` until the
  ≥ 3 docs + ≥ 1 plan bar is met.
- File a plan by outcome instead of state (a shipped plan sitting in `proposed/`).
- Leave a status line stale ("Proposed" over code that shipped).
- Nest deeper than `docs/<domain>/<bucket>/<category>/file.md`.

✅ **Do:**
- Let the domain and bucket carry context; keep file names short.
- Move a plan between `plans/` buckets as its state changes.
- Keep a superseded doc, moved to `archive/` with a note pointing at what replaced it.
- Promote a subsystem out of `main` once it earns its own domain.

---

## How this layout came to be

The tree was reorganized on 2026-07-21 from an earlier flat
`roadmap/` + `tale/` split. Every one of the 96 documents was classified by an
agentic workflow (cheap per-file typing → codebase evidence-gathering → a single
consolidating pass that discovered the domain roster and weighed each plan's
claims against the code). The full audit trail — domain roster, the suspicious
docs whose bodies contradict the code, cross-cutting duplicates, and pre-existing
broken links — lives in `docs/_migration/REVIEW.md`.

---

## Document history

- **2026-07-23**: Rewritten to document the domain-first layout as built.
- **2026-04-10**: Created initial PROCESS_DOCS.md proposing a purpose-first structure
  (superseded — the structure actually adopted is domain-first).
