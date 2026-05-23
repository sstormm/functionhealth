# CLAUDE.md

This file is loaded automatically by Claude Code at the start of every session in this repo. Keep it short; the full plan lives in `docs/BUILD_PLAN.md`.

## What this project is

A Function Health Full Stack take-home: a small todo app with .NET 10 Web API + Dapper + SQLite on the backend, React + TypeScript + Vite on the frontend. The build is governed by **`docs/BUILD_PLAN.md`**.

## Before doing any work

1. Read `docs/BUILD_PLAN.md` end to end. The critical sections are:
   - §1 — Operating principles (read these first; they shape every other decision)
   - §6 — What is NOT in scope (do not start these features)
   - §8 — Phased plan with checkpoints (the build order is fixed)
2. Skim `docs/design-handoff.md` for UI/UX layout and interaction reference.

## Conflict resolution

- `docs/BUILD_PLAN.md` overrides `docs/design-handoff.md` wherever they conflict. The design intentionally includes features that have been scoped out for time (drag-and-drop reorder, ⌘K palette, sort modes, separate mobile patterns) — see `docs/BUILD_PLAN.md` §6.
- `docs/BUILD_PLAN.md` overrides this `CLAUDE.md` wherever they conflict.

## Two principles that override most other defaults

The prep guide that shaped this project calls out two failure modes that get candidates rejected. They override standard "best practice" instincts:

1. **The full-circle rule.** Every feature must be complete end-to-end (user action → frontend → backend → DB → frontend feedback). A half-built feature counts against the submission *more* than a feature honestly chosen not to build. If a phase cannot be finished cleanly, **stop and polish what you have** — do not leave a partial feature visible in the UI.
2. **No scaffolding without purpose.** One backend project. No repository layer wrapping Dapper. No CQRS/MediatR. No extra projects. No CI pipelines or Dockerfiles. Each layer of structure must justify itself; on a CRUD todo app, almost none can. Controllers call Dapper directly via the injected `ISqliteConnectionFactory`.

## Code style

- **Favor immutable types where they fit.** Default to `record` (or classes with `init`-only setters) for data carriers — DTOs, response shapes, request bodies, Dapper query-result rows. Reach for mutable classes only when something downstream genuinely requires mutation.
- **Command/query separation: a function either calculates a result or has a side effect, not both.** A `Map(row)` returns a DTO and touches nothing else. A `BumpListUpdatedAt(listId)` writes to the DB and returns a status result type. Helpers that quietly mutate state while also returning a value are the failure mode this is guarding against. HTTP controller actions are the natural boundary where return + side effect coexist — that's fine; the principle applies to the helpers underneath them.

## Workflow expectations

- Work phase by phase per `docs/BUILD_PLAN.md` §8. At each checkpoint, stop and run the manual verification steps in the plan before moving on. Do not advance past a checkpoint with a failing or partial item.
- Do not add features that aren't in `docs/BUILD_PLAN.md` without flagging it to the user first.
- Do not add scaffolding (extra projects, abstraction layers, CI, containers) without flagging it first.
- The submission README (described in `docs/BUILD_PLAN.md` §9) is part of the deliverable, not an afterthought. Keep it honest about what's built and what's deliberately not built.
- **Keep docs aligned with code.** Whenever a code change is identified — before making it, or right after — review `README.md` and `docs/BUILD_PLAN.md` and update them so they reflect the new behavior, contracts, or scope. The check is required; the update is conditional on whether anything actually drifted (a pure internal refactor with no behavior or contract impact won't need either). Treat doc drift as part of the change, not a follow-up.

## Build progress tracking

There is (or will be) a file at `docs/claude_build_tracking.md` that is the canonical record of build progress. It is your responsibility to create and maintain it across every session.

**On your first session in this repo** (the file does not yet exist):

1. Read `docs/BUILD_PLAN.md` and `docs/design-handoff.md` fully.
2. Generate `docs/claude_build_tracking.md` with this structure:
   - A brief intro paragraph stating what the file is ("Running record of build steps for this repo. Maintained by Claude Code. See `docs/BUILD_PLAN.md` for the spec.").
   - A numbered, named step list breaking the full build into discrete sequential steps. Each step:
     - has a short descriptive name (e.g. "Backend: lists controller + endpoints")
     - cites the `docs/BUILD_PLAN.md` section(s) it maps to
     - is a single coherent unit of work that ends in a runnable / testable state
     - is small enough that the user can choose to stop after any one step
   - Each step is a markdown task-list item with a status marker:
     - `[ ]` not started
     - `[x]` done (and verified per its BUILD_PLAN checkpoint)
     - `[~]` skipped or deferred — append ` — *skipped: <one-line reason>*` to the name
3. Do not write any other code yet. Wait for the user to say "go through step N."

**On every subsequent session**, before doing any work:

1. Read `docs/claude_build_tracking.md` to learn which steps are done, skipped, or pending.
2. Do not silently re-do completed steps. If you think a completed step needs re-doing, flag it to the user and wait.

**As you work:**

- When you complete a step, mark it `[x]` in the tracking file and commit (if commits are part of your flow).
- When the user tells you to skip a step, mark it `[~]` with the reason inline.
- When the user revises or adds a step, update the file to match. Keep the numbering stable where possible.
- The "go through step N" instruction means: execute every not-yet-done step from the current state up to and including step N, then stop and report what you finished. Do not advance past step N without a new instruction.

`docs/BUILD_PLAN.md` is the spec; `docs/claude_build_tracking.md` is the live status.

## File locations

- `docs/BUILD_PLAN.md` — the plan (read this first)
- `docs/design-handoff.md` — UI/UX reference
- `backend/TodoApi/` — the .NET 10 project
- `backend/TodoApi.Tests/` — xUnit tests
- `frontend/todo-web/` — the React + Vite project
- `README.md` — the submission README (per `docs/BUILD_PLAN.md` §9)
