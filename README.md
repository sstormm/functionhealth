# Todo App — Function Health Take-Home

A small full-stack todo app. **.NET 10 Web API + Dapper + SQLite** on the backend; **React + TypeScript + Vite** on the frontend.

---

### Prereqs

- **.NET 10 SDK** (`dotnet --list-sdks` should show `10.0.x`)
- **Node 20 or later** (`node --version`)

## Quick start

Open two terminals.

**Terminal 1 — backend:**

```sh
cd backend/TodoApi
dotnet run
```

Serves on `http://localhost:5000`. On first run it creates `todo.db` in the working directory and seeds two demo users.

**Terminal 2 — frontend:**

```sh
cd frontend/todo-web
npm install   # first time only
npm run dev
```

Serves on `http://localhost:5173`.

Open `http://localhost:5173`, sign in with one of the demo accounts below, and you're in.

### Demo accounts

| Username | Password |
|---|---|
| `alice@example.com` | `alice123` |
| `bob@example.com` | `bob123` |

Each demo user is seeded with a list containing one open item and one completed item so the app opens to a populated list rather than the empty state. The first list in the sidebar (A-Z) is auto-selected on initial load.

### Running the tests

```sh
cd backend/TodoApi.Tests
dotnet test
```

Each test runs against a fresh SQLite file, so the suite is fully isolated and order-independent.

### Resetting the local data

The SQLite database lives at `backend/TodoApi/todo.db` (gitignored). Delete it and restart the backend to reseed with fresh demo data:

```sh
rm backend/TodoApi/todo.db
```

### Configuration

Backend settings live in [`backend/TodoApi/appsettings.json`](backend/TodoApi/appsettings.json) and follow standard ASP.NET Core config precedence (later wins):

1. `appsettings.json` — production-safe defaults, committed. Holds only environment-agnostic settings (logging, allowed hosts). Notably, **no `Cors:AllowedOrigins` and no `ConnectionStrings:Sqlite`** — the app fails closed at startup if no environment supplies them, so production can't accidentally fall back to a dev origin or write a SQLite file to whatever the deploy host's CWD happens to be.
2. `appsettings.{Environment}.json` — e.g., `appsettings.Development.json` (committed; contains the `http://localhost:5173` Vite-dev origin and the `Data Source=todo.db` connection string), or `appsettings.Production.json` (would be committed per-environment with the prod origin and connection string).
3. Environment variables — `Cors__AllowedOrigins__0=https://todo-app.com` or `ConnectionStrings__Sqlite=Data Source=/var/lib/todo/prod.db` overrides at deploy time.

For a real deployment you'd either commit an `appsettings.Production.json` with the production SPA origin and connection string or set the env vars at deploy time:

```json
// appsettings.Production.json
{
  "Cors": { "AllowedOrigins": [ "https://todo-app.com" ] },
  "ConnectionStrings": { "Sqlite": "Data Source=/var/lib/todo/prod.db" }
}
```

The frontend's API base URL is currently still hardcoded in [`frontend/todo-web/src/api.ts`](frontend/todo-web/src/api.ts); making that env-driven (via a Vite `VITE_API_BASE` build-time variable) would be the matching frontend change. Tracked in "What I'd do with another day."

---

## What I built

I used Claude Design to come up with the app views, layouts and features. I had it generate a handoff document that I then used with Claude Cowork (along with the take-home test PDFs provided by Paola) to work through a detailed plan that would be used by Claude Code. Cowork output a Claude.md file and BUILD_PLAN.md file that were provided to Claude Code to do the development. I left the resources that were used in the ```/docs``` folder for transparency. I reviewed the code, configuration, db, etc., did testing, and used Claude Code to refine the app and fix issues. Because I used Claude, there are more tests than I would written by hand but I've reviewed them and they seem reasonable (I'll own it).

I thought the app needed to have multiple lists--I use google keep all the time and wanted this app to have enough substance to not be totally throwaway.

App features that were included:

- **Username/password auth** with PBKDF2 hashed passwords (`PasswordHasher<T>` from `Microsoft.AspNetCore.Identity`) and opaque session tokens sent via `Authorization: Bearer`. Sessions use a **24-hour sliding expiration**: every authenticated request extends the session if less than 12 hours remain, via a single conditional `UPDATE` — so most requests don't write. Failed-login paths are **constant-time**: PBKDF2 verify runs against a static dummy hash when the user doesn't exist, so timing can't distinguish "no such user" from "wrong password." Session tokens are **stored as SHA-256 hashes** server-side — the raw token leaves the server exactly once (in the login response) and is never persisted, so database compromise doesn't leak live tokens.
- **Multi-list CRUD** with per-user, case-insensitive name uniqueness; trimmed-and-non-blank validation; inline error UI for blank names and duplicates.
- **Item CRUD** with completion state, completed bin sorted by `CompletedAt DESC`, open items sorted by `Order` then `CreatedAt`, inline-edit on click, hover-to-delete, and a non-optimistic mutation flow (button shows a brief disabled state; UI updates only after the server response).
- **Responsive layout** with a single `@media (max-width: 768px)` breakpoint — the sidebar collapses to a slide-in drawer toggled by a hamburger in the top bar.
- **System-error handling** with three distinct paths: per-request errors render inline at their source; session expiry silently redirects to `/login` (no banner — stale tokens after a backend restart are common, the message just added noise); network failures (fetch throws) show a blocking "Unable to reach server" modal whose Retry button probes `/api/auth/me`; 5xx responses show a blocking modal whose **OK** runs a soft-reload (`/me` → `/lists` → `/items`) and either dismisses or bounces to `/login?reason=system_error`. No silent failures.
- **Backend integration tests** — 53 covering every endpoint, including:
  - **Ownership enforcement.** Every cross-user access returns `404` (not `403` — we don't leak existence).
  - **Sliding session refresh.** Two named tests prove the refresh fires when ≤12h remain and *doesn't* fire when >12h remain — important for the perf claim above.

---

## What I deliberately left out

- **Account creation / password reset.** Two seeded users are sufficient to demonstrate auth + ownership.
- **Tagging/categories.** IMO this gets out of hand quick. Multiple 'to do' lists is enough.
- **Due dates.** Overkill for a to do list. If the assignment was "task management" then maybe I'd consider including.
- **Search.** Client-side search across list names and item text. A simple top-bar input with debounced filtering, no separate palette UI. Spec'd, not shipped.
- **Item reordering.** Useful but didn't seem necessary for the MVP.
- **⌘K command palette.** The wireframe shows a grouped, keyboard-navigable search palette with substring highlighting. The plan replaces it with a simpler top-bar search box. *Not shipped in the current submission state.*
- **Sort modes (A-Z / Recent).** Lists are sorted A-Z server-side. The user-facing toggle adds state surface for marginal value.
- **Separate mobile patterns** — bottom sheets, full-screen mobile search sheet. The single responsive layout works on phones; bespoke mobile patterns would roughly double the UI work.
- **Real-time sync / multi-tab consistency.** The frontend assumes it's the only writer per tab. Editing in two tabs at once will produce last-write-wins behavior with no live updates.
- **Frontend component tests.** The highest-risk surface is on the backend and is covered by integration tests there. The next-highest-value test surface would be the list-form modal's validation rules.

---

## What I'd do with another day

- **Bulk deletion** Clearing out open/completed items with one click
- **Drag-and-drop reorder** (HTML5 DnD or `@dnd-kit/core`) with a backend endpoint accepting a full ordered list of item IDs in one request, so reorders are atomic.
- **The full ⌘K palette** with grouped, keyboard-navigable results and substring highlighting.
- **Account creation + password reset.**
- **Standard security headers from the backend.** The API doesn't currently set `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, `X-Frame-Options` (or CSP `frame-ancestors`), or `Referrer-Policy`. In production these typically come from the edge layer (reverse proxy / CDN), but if the API is ever directly exposed they should travel with it. A starter `Content-Security-Policy` on the SPA's `index.html` would also harden the XSS surface (defense in depth on top of the SHA-256 token storage). A small middleware, no contract change.
- **Move the session token from `localStorage` to an `httpOnly` + `Secure` + `SameSite=Strict` cookie.** Today the token sits in `localStorage` and `api.ts` attaches it via `Authorization: Bearer` on each request — defensible for this scope (no CSRF surface, true logout, no JWT signing-key story) but exfiltratable by any JS that lands in the page: an XSS hit, a compromised dependency, a stray third-party script. An httpOnly cookie removes the exfiltration path entirely — JS can't read it; the browser auto-attaches it. The cleanest deployment pairs this with a same-origin reverse proxy (SPA + API behind one host) so `SameSite=Strict` solves CSRF without a separate token. The hardened version most fintech SPAs use splits this into a short-lived in-memory access token + a long-lived httpOnly refresh cookie, limiting blast radius if XSS does land — the access token expires in minutes and refreshing requires the cookie JS can't see.
- **Optimistic UI updates** (e.g., via React Query) so mutations feel instant. Deliberately skipped because pairing optimistic updates with the system-error modals would require a "remember what to revert" layer; the current "brief disabled state, update on response" pattern stays correct under all failure modes.
- **Frontend component tests** for the modals (the `ListFormModal` validation rules are the highest-value test target).
- **A small CI workflow** (GitHub Actions) running `dotnet test` and `npm run build` on PR.

---

## Assumptions

- Two seeded users is sufficient demonstration of multi-tenant ownership for this exercise.
- File-based SQLite is acceptable (the brief lists "SQL Lite or EF Core in memory" — chose file-backed SQLite so data survives restart, with Dapper as the data-access layer instead of EF Core).
- Session tokens are opaque random strings whose SHA-256 hashes are stored in a `Sessions` table (chosen over JWT so logout truly invalidates and so there is no signing-key story to explain — both meaningful for a small app).
- Deletes are permanent; the wireframes' "This can't be undone" copy matches.
- The backend runs on `http://localhost:5000` (HTTP only — `app.UseHttpsRedirection()` is removed in dev to skip the local-cert dance). The frontend hardcodes this base URL.

---

## Architecture notes

- **One backend project.** Controllers call Dapper directly via an injected `ISqliteConnectionFactory`. No repository layer wrapping Dapper (Dapper is already thin enough). No `Application/Domain/Infrastructure` split.
- **The schema lives in one place** — `Db/Schema.cs` as raw SQL, applied idempotently at startup and per test fixture. No migration framework — the app has one schema version.
- **SQLite foreign keys are explicitly enabled per connection** (`PRAGMA foreign_keys = ON` inside `SqliteConnectionFactory.OpenConnection`); without this, `ON DELETE CASCADE` silently doesn't fire — a footgun specific to SQLite.
- **Errors use a single consistent shape** — `{ "error": { "code", "message" } }`. The frontend specifically branches on three codes (`INVALID_CREDENTIALS`, `DUPLICATE_NAME`, `NOT_FOUND`); for everything else it renders the server's `message` verbatim.
- **Ownership is enforced inline at every query.** Each list query starts with `WHERE UserId = @currentUserId`; each item query scopes via `WHERE i.ListId IN (SELECT Id FROM Lists WHERE UserId = @currentUserId)`. There is no place in the code where this filter can be bypassed — verified by the ownership tests (e.g., `RenameList_OtherUsersList_Returns404`, `UpdateItem_OtherUsersItem_Returns404`).
- **Data carriers are `record` types.** All DTOs, request/response shapes, and Dapper query-result row types are records with `init`-only setters; functions follow command/query separation where possible (HTTP handlers are the exception by nature).
- **Frontend state is local `useState` + a small `api.ts` wrapper around `fetch`.** No Redux, no React Query. The api client publishes system errors to a tiny pub/sub store; a single `ErrorOverlay` mounted at the app root subscribes and renders the right modal.

---

Scope reasoning, phased plan, and explicit non-goals are documented in [`docs/BUILD_PLAN.md`](docs/BUILD_PLAN.md) for transparency.
