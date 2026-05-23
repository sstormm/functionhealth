# Function Health Take-Home — Build Plan

A spec + phased plan for Claude Code to build the Full Stack Developer take-home from Function Health. **Read the "Operating principles" section before writing any code — the prep guide is unusually explicit about what gets candidates rejected, and this plan is shaped around those rules.**

---

## 1. Operating principles (read first, do not skip)

These come directly from the Function Health prep guide. They override anything you might consider "best practice" elsewhere:

1. **The full-circle rule.** Every feature you build must be complete end-to-end: user action → frontend → backend → database → backend → frontend → user feedback. If any point in that loop is broken or missing, the feature is *not* shipped. A half-built feature counts against the submission *more* than a feature honestly chosen not to build.
2. **No scaffolding without purpose.** One backend project. No repository layer wrapping Dapper (Dapper is already thin enough that wrapping it just adds indirection). No MediatR, no CQRS, no Application/Domain/Infrastructure split. No CI pipelines, no Dockerfiles, no observability. Each layer of structure is a claim that the app needs it; on a CRUD todo app, almost all such claims are false.
3. **Data must survive restart.** Use file-backed SQLite. The assignment lists "SQL Lite or EF Core in memory" as the two options — in-memory would mean data vanishes on restart, which the prep guide explicitly flags as a demo signal. "A filing cabinet that empties itself is not a filing cabinet."
4. **Validation everywhere.** Every input the API accepts is validated. Every error gets a real user-facing response in the UI, not a silent crash.
5. **Auth is included, scoped, and tested.** Two seeded users. Hashed passwords. Session token issued on login. Every list/item endpoint enforces ownership. There must be at least one test that proves User A cannot reach User B's data.
6. **README is a trust signal.** Honest about what's built, what's deliberately not built, and what would come next. Pretending something is complete when it isn't is worse than leaving it out.
7. **What you're not building.** Sort modes (A-Z / Recent), drag-and-drop reorder, ⌘K command palette, separate mobile patterns (bottom sheets, full-screen mobile search sheet), account creation, password reset. These are deferred to the README's "future work" section. Do not start them.
8. **Build order is fixed.** Phase 1 (auth + lists/items CRUD with validation + tests) must be 100% complete before Phase 2 begins. Phase 2 (item reorder via Move-to-top/bottom) must be complete before Phase 3 (simple search). If a phase cannot be finished cleanly, stop, polish what you have, and document the cut in the README. Do *not* leave a half-built feature visible in the UI.

---

## 2. Tech stack

| Layer | Choice | Notes |
|---|---|---|
| Backend | **.NET 10 Web API**, single project | Controllers (not minimal API — controllers + DI feel less novel to reviewers and are easier to test). One `*.csproj` with `<TargetFramework>net10.0</TargetFramework>`. CORS allowed-origins, connection string, and other deploy-varying values come from `appsettings.json` + environment overrides — no environment-specific values are hardcoded. |
| Data access | **Dapper + Microsoft.Data.Sqlite (file-backed)** | `Data Source=todo.db` in the project working dir. No ORM. Schema is created at startup via idempotent `CREATE TABLE IF NOT EXISTS` statements (see §3.1). SQLite foreign keys are **off by default per connection** — the `SqliteConnectionFactory` must execute `PRAGMA foreign_keys = ON` on every connection it opens, otherwise cascade deletes silently won't fire. |
| Password hashing | `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` | Don't roll your own. PBKDF2-based, included with ASP.NET Core. Add the `Microsoft.AspNetCore.Identity` package just for this type — do NOT enable full Identity. |
| Session tokens | Opaque random tokens stored in a `Sessions` table, with sliding expiration | Token = base64-url of 32 random bytes from `RandomNumberGenerator`. Sent via `Authorization: Bearer <token>`. 24-hour TTL that slides forward on use — see §3.3. Logout truly invalidates by deleting the row. No JWT signing key to manage. |
| Tests | **xUnit** + `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) | Per-test SQLite file via `Path.GetTempFileName()`; schema applied via the same startup routine; users seeded by the fixture. Coverage: every endpoint (see §7). |
| Frontend | **React 18 + TypeScript + Vite** | One project. No Next.js, no SSR. |
| Frontend state | Local `useState` + a small `api.ts` module wrapping `fetch` | No Redux. Optional: a tiny custom hook per resource (`useLists`, `useItems`). React Query is fine if you want it, but plain `fetch` + `useEffect` is simpler and meets the bar. |
| Frontend styling | Plain CSS modules or a single global stylesheet | Match the wireframes' "Bootstrap-ish" feel (charcoal borders, warm off-white background, 3–4px radii). Tailwind is acceptable if it's faster for you. No component library unless it saves time. |
| Frontend layout | Flexbox + a single `@media (max-width: 768px)` breakpoint | Responsive web. Sidebar collapses to a hamburger drawer on narrow screens. **No** separate mobile sheets/components. |

### Folder layout (target final state)

```
repo/
├── README.md                           # primary submission doc — see §9
├── backend/
│   ├── TodoApi/
│   │   ├── TodoApi.csproj              # <TargetFramework>net10.0</TargetFramework>
│   │   ├── Program.cs                  # composition, middleware, DI, schema + seed at startup
│   │   ├── appsettings.json
│   │   ├── todo.db                     # gitignored
│   │   ├── Models/                     # POCOs (currently just User — phantom type for PasswordHasher<User>)
│   │   ├── Dtos/                       # request/response shapes
│   │   ├── Db/
│   │   │   ├── ISqliteConnectionFactory.cs   # OpenConnection() with PRAGMA foreign_keys=ON
│   │   │   ├── SqliteConnectionFactory.cs
│   │   │   └── Schema.cs               # static EnsureCreated(IDbConnection): CREATE TABLE IF NOT EXISTS …
│   │   ├── Controllers/                # AuthController, ListsController, ItemsController — call Dapper directly
│   │   └── Auth/
│   │       ├── SessionAuthHandler.cs   # custom AuthenticationHandler reading Bearer token
│   │       └── PasswordService.cs      # thin wrapper over PasswordHasher<User>
│   └── TodoApi.Tests/
│       ├── TodoApi.Tests.csproj
│       ├── TestFixture.cs              # WebApplicationFactory + per-test SQLite file + seeding helpers
│       ├── AuthControllerTests.cs
│       ├── ListsControllerTests.cs
│       ├── ItemsControllerTests.cs
│       └── MoveItemTests.cs            # added in Phase 2
└── frontend/
    └── todo-web/
        ├── package.json
        ├── vite.config.ts
        ├── index.html
        ├── src/
        │   ├── main.tsx
        │   ├── App.tsx                 # route guard + mounts ErrorOverlay at root
        │   ├── api.ts                  # fetch wrapper; emits request / session_expired / system errors
        │   ├── auth.ts                 # token storage, isAuthed(), goToLoginWithReason()
        │   ├── systemError.ts          # SystemErrorStore (tiny pub/sub) + useSystemError() hook
        │   ├── types.ts                # shared TS types (List, Item, etc.)
        │   ├── styles.css
        │   ├── pages/
        │   │   ├── LoginPage.tsx
        │   │   └── AppPage.tsx         # the post-login screen (sidebar + main pane)
        │   └── components/
        │       ├── Sidebar.tsx
        │       ├── ListPane.tsx
        │       ├── ItemRow.tsx
        │       ├── AddItemRow.tsx
        │       ├── Modal.tsx           # generic centered modal
        │       ├── ListFormModal.tsx   # used by Create + Rename
        │       ├── DeleteListModal.tsx
        │       └── ErrorOverlay.tsx    # the network / server modals per §5.6
```

---

## 3. Data model

```text
User
  Id              GUID (PK)
  Email           string  (unique, lowercased on insert)
  PasswordHash    string  (PBKDF2 via PasswordHasher<User>)
  CreatedAt       UTC timestamp

Session
  Token           string  (PK; SHA-256 hex of the raw token. Raw token is 32 CSPRNG bytes
                            base64url-encoded; it leaves the server only in the login
                            response and is never persisted. Inbound requests are
                            re-hashed and looked up by hash. DB compromise yields no
                            usable live tokens.)
  UserId          GUID    (FK → User.Id)
  CreatedAt       UTC timestamp
  ExpiresAt       UTC timestamp  (set to now + 24h on creation; slides forward on use — see §3.3)

List
  Id              GUID (PK)
  UserId          GUID (FK → User.Id, indexed)
  Name            string  (non-empty, trimmed, unique per user case-insensitive)
  CreatedAt       UTC timestamp
  UpdatedAt       UTC timestamp  (bumped on rename and on any child-item create/edit/delete/complete)

Item
  Id              GUID (PK)
  ListId          GUID (FK → List.Id, indexed)
  Text            string  (non-empty, trimmed, max 500 chars)
  Completed       bool    (default false)
  Order           int     (manual order within open items; ignored when Completed = true)
  CompletedAt     UTC timestamp? (set when Completed → true; cleared on uncomplete)
  CreatedAt       UTC timestamp
  UpdatedAt       UTC timestamp
```

**Notes:**
- `Order` only matters for open items. When sorting open items: `ORDER BY [Order] ASC, CreatedAt ASC`. Completed items: `ORDER BY CompletedAt DESC`.
- When an item is completed: `Completed = 1`, set `CompletedAt = now`, leave `Order` as-is (don't bother re-packing). When uncompleted: `Completed = 0`, `CompletedAt = NULL`, and assign `Order = max(open Order in list) + 1` so it appears at the bottom of the open list.
- Timestamps are stored as UTC ISO-8601 strings (`TEXT` in SQLite). The frontend converts to local time only for display.
- Cascade: deleting a List deletes its Items. Deleting a User deletes their Lists and (transitively) their Items. Enforced via `FOREIGN KEY ... ON DELETE CASCADE` in the schema — but **only works if `PRAGMA foreign_keys = ON` is set per connection**. The `SqliteConnectionFactory` is responsible for this.
- Seed two users in `Program.cs` on first run (only if the `Users` table is empty):
  - `alice@example.com` / password `alice123`
  - `bob@example.com` / password `bob123`
  - Also seed one starter list ("Today") with one open item ("costco run") and one completed item ("finish take home interview project") per seeded user, so the demo opens to a populated list rather than the empty state.

### 3.1 SQL schema (source of truth)

These statements live in `Db/Schema.cs` and run idempotently at startup (and again per test, against the temp DB).

```sql
CREATE TABLE IF NOT EXISTS Users (
    Id            TEXT    PRIMARY KEY,
    Email         TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash  TEXT    NOT NULL,
    CreatedAt     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Sessions (
    Token      TEXT  PRIMARY KEY,
    UserId     TEXT  NOT NULL,
    CreatedAt  TEXT  NOT NULL,
    ExpiresAt  TEXT  NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Sessions_UserId ON Sessions(UserId);

CREATE TABLE IF NOT EXISTS Lists (
    Id         TEXT  PRIMARY KEY,
    UserId     TEXT  NOT NULL,
    Name       TEXT  NOT NULL,
    CreatedAt  TEXT  NOT NULL,
    UpdatedAt  TEXT  NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Lists_UserId ON Lists(UserId);
-- Per-user case-insensitive name uniqueness:
CREATE UNIQUE INDEX IF NOT EXISTS UX_Lists_UserId_Name ON Lists(UserId, Name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS Items (
    Id           TEXT     PRIMARY KEY,
    ListId       TEXT     NOT NULL,
    Text         TEXT     NOT NULL,
    Completed    INTEGER  NOT NULL DEFAULT 0,   -- 0/1
    "Order"      INTEGER  NOT NULL DEFAULT 0,   -- quoted because ORDER is reserved
    CompletedAt  TEXT     NULL,
    CreatedAt    TEXT     NOT NULL,
    UpdatedAt    TEXT     NOT NULL,
    FOREIGN KEY (ListId) REFERENCES Lists(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Items_ListId ON Items(ListId);
```

### 3.2 Dapper usage pattern

Controllers receive an `ISqliteConnectionFactory` via DI and use Dapper directly — no repository wrapper. Pattern looks like:

```csharp
[HttpGet]
public async Task<IActionResult> GetLists()
{
    var userId = User.GetUserId();
    using var conn = _connFactory.OpenConnection();
    var lists = await conn.QueryAsync<ListResponse>(@"
        SELECT l.Id, l.Name, l.UpdatedAt,
               (SELECT COUNT(*) FROM Items i WHERE i.ListId = l.Id AND i.Completed = 0) AS OpenCount,
               (SELECT COUNT(*) FROM Items i WHERE i.ListId = l.Id AND i.Completed = 1) AS DoneCount
        FROM Lists l
        WHERE l.UserId = @userId
        ORDER BY l.Name COLLATE NOCASE ASC",
        new { userId });
    return Ok(lists);
}
```

Ownership enforcement is enforced inline by every query starting with `WHERE UserId = @currentUserId` (lists) or `WHERE ListId IN (SELECT Id FROM Lists WHERE UserId = @currentUserId)` (items). There is no place in the code where this filter can be skipped — that's the property the ownership tests verify.

### 3.3 Session lifecycle (sliding expiration)

Sessions use a sliding 24-hour TTL with a 12-hour refresh threshold. The behavior is implemented entirely in `SessionAuthHandler` — no extra schema, no extra endpoints.

**Constants** (defined in `SessionAuthHandler`):
```csharp
private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(24);
private static readonly TimeSpan RefreshThreshold = TimeSpan.FromHours(12); // = SessionTtl / 2
```

**On login**: insert the session row with `ExpiresAt = now + SessionTtl`.

**On every authenticated request** (inside `HandleAuthenticateAsync`), pseudo-code:
```csharp
var session = await conn.QuerySingleOrDefaultAsync<Session>(
    "SELECT Token, UserId, ExpiresAt FROM Sessions WHERE Token = @token", new { token });
if (session is null) return AuthenticateResult.Fail("Invalid token");

var now = DateTime.UtcNow;
if (session.ExpiresAt <= now) return AuthenticateResult.Fail("Token expired");

// Sliding refresh: only write when less than half the TTL remains.
if (session.ExpiresAt - now < RefreshThreshold)
{
    var newExpiresAt = now + SessionTtl;
    await conn.ExecuteAsync(
        "UPDATE Sessions SET ExpiresAt = @newExpiresAt WHERE Token = @token",
        new { newExpiresAt, token });
}

// Build ClaimsPrincipal with NameIdentifier = session.UserId and return Success.
```

**Why this shape:**
- *Simple.* One conditional UPDATE; no separate `LastSeenAt` column, no refresh-token plumbing.
- *Efficient.* Typical authenticated request = 1 SELECT only. Refresh only fires when more than 12 hours have elapsed since the last refresh — at most once per ~12h per active user, regardless of request volume.
- *Race-safe.* Two concurrent requests within the threshold may both fire the UPDATE; both write the same `now + 24h`, last-write-wins, no corruption.
- *No absolute lifetime cap.* Sessions can slide indefinitely as long as the user stays active. Adding a hard ceiling (e.g., `CreatedAt + 30d`) would require a second column and an additional comparison; deliberately deferred to the README's "future work" section as the natural production-hardening step.

**Logout** still works exactly as before: `DELETE FROM Sessions WHERE Token = @token`. Sliding doesn't change that path.

---

## 4. API contract

All endpoints under `/api`. All responses are JSON. All non-auth endpoints require `Authorization: Bearer <session-token>`; missing or invalid token returns `401`. Any endpoint that touches a resource not owned by the token's user returns `404` (not `403` — we prefer not to leak existence).

### Auth

| Method | Path | Body | Success | Errors |
|---|---|---|---|---|
| POST | `/api/auth/login` | `{ email, password }` | `200 { token, user: { id, email } }` | `400` malformed body; `401` bad creds |
| POST | `/api/auth/logout` | — | `204` | `401` if not authed |
| GET  | `/api/auth/me`     | — | `200 { id, email }` | `401` if not authed |

### Lists

| Method | Path | Body | Success | Errors |
|---|---|---|---|---|
| GET    | `/api/lists`            | — | `200 [ { id, name, updatedAt, openCount, doneCount } ]` (sorted by `name ASC`) | `401` |
| POST   | `/api/lists`            | `{ name }` | `201 { id, name, ... }` | `400` blank, `409` duplicate (case-insensitive within user) |
| PATCH  | `/api/lists/{id}`       | `{ name }` | `200 { id, name, ... }` | `400` blank, `404`, `409` duplicate |
| DELETE | `/api/lists/{id}`       | — | `204` | `404` |

### Items

| Method | Path | Body | Success | Errors |
|---|---|---|---|---|
| GET    | `/api/lists/{listId}/items` | — | `200 { open: [...], completed: [...] }` (open sorted by `Order ASC, CreatedAt ASC`; completed sorted by `CompletedAt DESC`) | `404` if list not owned |
| POST   | `/api/lists/{listId}/items` | `{ text }` | `201 { id, text, completed:false, order, ... }` (assign `Order = max + 1`) | `400` blank or >500 chars, `404` |
| PATCH  | `/api/items/{id}`           | `{ text?, completed? }` (partial) | `200 { ... }` | `400`, `404` |
| DELETE | `/api/items/{id}`           | — | `204` | `404` |
| POST   | `/api/items/{id}/move`      | `{ position: "top" \| "bottom" }` | `200 { id, order, ... }` | `400` invalid position; `404`; `409` if completed (cannot reorder completed items) |

**Error response shape** (consistent across the API):
```json
{ "error": { "code": "DUPLICATE_NAME", "message": "A list named \"Reading\" already exists." } }
```
Codes the frontend will key off: `BLANK_NAME`, `BLANK_TEXT`, `TEXT_TOO_LONG`, `DUPLICATE_NAME`, `INVALID_CREDENTIALS`, `UNAUTHENTICATED`, `NOT_FOUND`, `ITEM_COMPLETED`.

**Validation rules (server-side, always enforced):**
- `email`: non-empty, contains "@", trimmed, lowercased
- `password`: non-empty (don't bother with strength rules — these are seeded)
- list `name`: trimmed, non-empty, ≤ 100 chars, unique per user case-insensitive
- item `text`: trimmed, non-empty, ≤ 500 chars

---

## 5. Frontend screens (in-scope)

All screens use the wireframes as the source of truth for layout and interaction. Visual styling can be minimal — the prep guide explicitly does not reward "visual polish beyond clear and usable."

### 5.1 Login (`/login`)

- Centered card, ~340px wide, on neutral background.
- Username input (`type="text"` — see note below), password input, primary "Sign in" button.
- On submit: call `POST /api/auth/login`.
  - Success → store token in `localStorage`, navigate to `/`.
  - `401` → show inline error banner above the button: "That username & password don't match. Try again." Password input gets red border. Error clears as the user types.
  - Other `4xx` (e.g., genuinely missing fields → `400 MALFORMED_BODY`) → show a generic "Sign in failed. Please try again." in the same slot; no red border.
- The field is labeled "Username" in the UI even though the backend column is `Email`. Seeded credentials are email-formatted, but the login flow does not require an `@`: any malformed identifier returns the same generic 401 as a wrong password (see §1.5 / §7.1 — no information leak about which field was bad).
- **Login errors stay inline** — the LoginPage handles `kind: 'system'` (network / 5xx) failures in the same inline error slot, not via the §5.6 global overlay. The api client suppresses the `SystemErrorStore` publish for the login call specifically; the user is at the entry point with no in-progress state to recover, so a blocking modal is the wrong grain. §5.6's modal behavior remains correct for the post-auth surface.
- **Reason banner** (occupies the same inline error slot as the credential error): if the URL contains `?reason=<value>`, render the corresponding message on mount and dismiss it as soon as the user starts typing in either input (same dismissal behavior as the credential error). Supported reasons:
  - `?reason=session_expired` → "Your session has ended. Please sign in again."
  - `?reason=system_error` → "The system is having problems at the moment. Please try again in a few minutes."
  - Unknown reason or no reason → no banner shown.
  - Submitting the form (whether or not it succeeds) also dismisses the reason banner. The credential error takes over on a 401.
- No "Forgot password" / "Create account" links (those endpoints don't exist — don't show links that go nowhere).
- Do **not** display the demo credentials on the page. Even though the accounts are seeded and harmless, putting credentials in the UI is a production-ready posture failure. Demo accounts live in the README only.

### 5.2 Main app (`/`, protected)

Three regions, full viewport:

**Top bar** (~42px): wordmark "todo" left, user pill right (avatar circle + email + ▾).
- Click user pill → small popover containing a single "Sign out" button (red text). The user's email is already visible in the pill itself, so the "Signed in as <email>" label was dropped as redundant.
- Sign out: call `POST /api/auth/logout` first (while the token is still valid so the server-side session row is deleted), then clear the token and `activeListId` from `localStorage`, then navigate to `/login` (no `?reason=` — this is a deliberate sign-out, not a forced bounce).

**Sidebar** (left, ~220px on desktop, becomes a slide-in drawer on `max-width: 768px`):
- Header: "LISTS" label (small caps muted) + "+" button.
- Below: list rows, each `{ name … openCount }`, sorted A-Z (no sort dropdown in v1 — see Non-Goals).
- Active list row: orange tint background + orange left-border accent.
- "+" button → opens Create List modal (§5.3).
- Sidebar scrolls independently when there are more lists than fit.

**Main pane** (right, fills remaining width):
- On initial load, restore the remembered `activeListId` from `localStorage` if it still exists; otherwise auto-select the first list (A-Z). The "no list selected" empty state below is reachable only when the user has zero lists.
- If no list selected: show empty state ("No lists. Create one with +.") — gated on the initial fetch having completed so the copy doesn't briefly render during load.
- If a list is selected, top to bottom:
  1. **List header**: list name (h2) + meta "N open · M done" + right-aligned "Rename" / "Delete list" buttons.
  2. **Add-item row**: text input "Add an item…" + "Add" button. ↵ in input also adds. Validation: client disables Add if input is blank/whitespace; server rejects with `BLANK_TEXT` if somehow bypassed, and the row shows the error message inline.
  3. **Open items** card: each row = `[checkbox] [text]`. Click text → enter inline edit mode (input replaces text; ↵ saves, Esc cancels). Hover row → reveal a small delete (×) on the right.
  4. **Completed divider**: `--------- Completed · N ---------` (always visible).
  5. **Completed items** card: each row = `[checkbox-checked] [text with line-through, muted]`. Click checkbox → uncomplete (row moves back to open at bottom). Delete (×) available on hover.
- The whole main pane (list header, add-item row, items, completed bin) scrolls together as one unit when the viewport is short — the original "items scroll, header and add-item row pinned" pattern felt off because the input box ended up stranded at the top while everything else moved.

### 5.3 Create / Rename list modal

- Same component handles both. Centered card over dimmed backdrop.
- Title: "New list" or "Rename list".
- Single labeled text input ("List name"). Rename: prefilled, selected. Create: empty, placeholder "e.g. Reading".
- Inline error slot under the input (red border on input + red message below).
- Validation rules (live, on every keystroke — the frontend computes these so the user gets feedback without round-tripping):
  - Blank → Save disabled, **no inline error message**. The disabled button is sufficient signal; showing "List name can't be blank." before the user has typed anything reads as scolding.
  - Matches another list (case-insensitive, trimmed) → `A list named "<name>" already exists.` Save disabled.
  - Rename: matches the original (no change) → Save disabled (no error message).
  - Otherwise → Save enabled.
  - Server-side still rejects blank with `400 BLANK_NAME` if the client somehow bypasses the disabled button; the client surfaces the server message inline in that case.
- ↵ submits, Esc cancels. Click outside backdrop also cancels.
- On submit: call the API. If the server returns a 409 the client didn't catch (race), show the same duplicate-name error.
- On success: close modal. For Create, the new list becomes the active list. For Rename, the header updates, sidebar entry updates, list stays active.

### 5.4 Delete list modal

- Centered card over dimmed backdrop.
- Copy: `Delete "<name>"?` then `<N> open items and any completed items will be permanently removed. This can't be undone.`
- Buttons: Cancel (secondary) and "Delete list" (red).
- On delete: call `DELETE /api/lists/{id}`. On success: close modal, remove from sidebar, set active list to the first remaining list (or none if empty).

### 5.5 Responsive behaviour (single breakpoint)

- `min-width: 769px` → desktop layout described above.
- `max-width: 768px`:
  - Sidebar becomes a hidden drawer that slides in from the left when the user taps a hamburger button placed in the top bar (left of the wordmark).
  - Drawer is dismissed by tapping the wordmark area or tapping a list row (selecting a list closes the drawer and shows the main pane).
  - Top bar, list header, add-item row remain full-width.
  - Modals fill the screen with edge padding (~16px) instead of being centered cards.
- Do NOT build: bottom sheets, full-screen mobile search sheet, mobile action menus. The above is all the mobile work in scope.

### 5.6 System error handling

The frontend distinguishes three categories of failure:

| Category | Trigger | UX |
|---|---|---|
| **Per-request error** (validation, 404, 409) | Specific 4xx response with a known `code` | Inline at the source of the action (form error, item-row error). Already covered in §5.1–§5.4 and §4. |
| **Session expired** | Any 401 on a previously-authenticated request | Clear local token; redirect to `/login?reason=session_expired`. The login page renders the reason banner per §5.1. |
| **System down** | Network error (fetch throws) OR any 5xx response | Blocking centered modal over a dimmed backdrop (see below). No part of the app behind the modal is interactive — that's why the modal exists; there is no separate "read-only mode" for the controls. |

**Note on optimistic updates:** the plan does NOT use optimistic UI updates. Buttons show a brief disabled/loading state during a mutation; the UI updates only after the server response. This is deliberate — without optimistic updates, a network failure mid-mutation never leaves the UI in a state that disagrees with the server, so there is nothing to revert when the system-error modal appears.

#### 5.6a — Network-unreachable modal

- **Trigger:** the `fetch` call throws (backend unreachable, DNS failure, CORS misconfig, browser offline).
- **Visual:** centered modal card (same component shape as the Delete-list modal) over a dimmed backdrop. The backdrop intercepts all clicks; nothing behind it is reachable.
- **Copy:** Title "Unable to reach server." Body: "Sorry for the inconvenience. Click Retry to try again."
- **Single action button: "Retry."**
- **Retry behavior (probe + dismiss):** clicking Retry issues a single `GET /api/auth/me` probe. There is no automatic background retry — the modal stays up until the user clicks Retry and the probe succeeds.
  - Probe returns 200 → dismiss the modal. The user re-issues their original action manually.
  - Probe returns 401 → session is invalid, silently redirect to `/login` (no banner).
  - Probe throws or returns 5xx → leave the modal up; the user can click Retry again.
- **Why probe rather than replaying the original request:** the original request is not retained anywhere. The user clicks the button again themselves. This avoids a "remember the failed request" state surface entirely.

#### 5.6b — Server-error modal

- **Trigger:** the response is 5xx (typically a DB exception or unhandled server-side error).
- **Visual:** same centered modal + dimmed backdrop as 5.6a.
- **Copy:** Title "Something went wrong on our end." Body: "The system is experiencing problems. Click OK to refresh your view."
- **Single action button: "OK."**
- **OK behavior (soft reload):** re-fetch the initial post-login app state in this order:
  1. `GET /api/auth/me` — verify session
  2. `GET /api/lists` — rebuild sidebar
  3. `GET /api/lists/{activeListId}/items` if there is a remembered active list, else skip
  - All three succeed → dismiss the modal, re-render the app with fresh data.
  - Any of the three returns 401 → clear token, redirect to `/login?reason=session_expired`.
  - Any of the three returns 5xx or throws → clear token, redirect to `/login?reason=system_error`. The user lands on the login screen with an explanation; if the backend is still broken, their login attempt will fail and the credential-error path takes over (which is acceptable — the login page becomes the universal "I don't know, try here" terminus).

#### 5.6c — Edge cases worth naming

- **5xx during the initial post-login load.** Same 5.6b modal. OK triggers the soft reload, which is itself the failing call. It will bounce to `/login?reason=system_error`. The user briefly sees the modal then lands on login with the explanation. Acceptable.
- **Both modals at once.** Not possible — the api client either throws (5.6a) or returns a 5xx response (5.6b), never both. Only one modal mounts at a time.
- **User in the middle of typing when a modal appears.** Their typed input is lost (modal blocks interaction; on resolve they may be looking at fresh server data). This is a known limit of the no-optimistic-updates approach; it would also be lost under optimistic-with-revert. README mentions it under "What I'd do with another day" as a candidate for a draft-restore mechanism.
- **There is no offline mode.** If the backend is genuinely unreachable for an extended period, the user sits on the network-down modal until the backend comes back or they close the tab. Documented honestly in the README.

#### 5.6d — Where this lives in code

- A single `ErrorOverlay` component renders the modal for both 5.6a and 5.6b, parameterized by `mode: 'network' | 'server'`.
- The `api.ts` client throws or rejects in a normalized shape that includes the failure category. A small `useSystemError()` hook reads the most recent failure from the api client and decides which (if any) modal to mount.
- The redirect helper (`auth.ts → goToLoginWithReason(reason)`) handles both `session_expired` and `system_error` redirects in one place.

---

## 6. What is NOT in scope (and the README must say so)

These are explicitly cut. Do not start them. The README "What I deliberately left out" section lists them with one-line rationales:

- **Sort modes** (A-Z / Recent). Lists sort A-Z server-side. No client toggle.
- **Drag-and-drop reorder.** Reordering happens via the "Move to top / bottom" affordance (Phase 2 — see §8). DnD adds significant client complexity and a backend reorder-array endpoint for little marginal value here.
- **⌘K command palette.** Replaced by a simple "Search items" input in the top bar (Phase 3). No grouped results, no keyboard nav, no highlights, no include-completed toggle, no inline-edit-from-search.
- **Separate mobile patterns** — bottom sheets, full-screen mobile search sheet, mobile action sheets. Replaced by responsive web + a hamburger drawer.
- **Account creation, password reset, "Forgot password".** Two seeded users; the README explains why.
- **Real-time sync / multi-tab consistency.** The frontend is the only writer per tab.
- **Soft delete / undo.** Deletes are permanent, matching the wireframes' "This can't be undone" copy.

---

## 7. Tests

Every backend endpoint gets test coverage. The prep guide's two named priorities — ownership enforcement and input validation — are folded into the per-endpoint tests below. Frontend tests are added at the end only if time remains; backend coverage is the bar.

### 7.1 Backend integration tests (xUnit + `WebApplicationFactory`)

Use a per-test fresh SQLite file (created with `Path.GetTempFileName()`, deleted on `IDisposable.Dispose`). The fixture seeds two users — `alice` and `bob` — and exposes helpers `LoginAsAlice() → token`, `LoginAsBob() → token`, and `Client(token)` that returns an `HttpClient` with the token attached.

Organize one test class per controller. Names follow the pattern `Endpoint_Condition_ExpectedResult`.

#### `AuthControllerTests.cs`
- `Login_ValidCredentials_Returns200WithToken`
- `Login_WrongPassword_Returns401WithInvalidCredentials`
- `Login_UnknownEmail_Returns401WithInvalidCredentials` (same shape as wrong password — don't leak whether the email exists)
- `Login_InvalidEmailFormat_Returns401WithInvalidCredentials` (same shape — don't leak which field was malformed)
- `Login_MalformedBody_Returns400`
- `Logout_AuthenticatedUser_Returns204AndInvalidatesToken` (call `me` after logout → 401)
- `Logout_NoToken_Returns401`
- `Me_AuthenticatedUser_ReturnsUserInfo`
- `Me_NoToken_Returns401`
- `Me_JunkToken_Returns401`
- `Me_ExpiredToken_Returns401` (insert a session row with `ExpiresAt` in the past)
- `Request_WhenSessionWithinRefreshThreshold_ExtendsExpiresAt` (seed a session with `ExpiresAt = now + 1h`, hit `/me`, assert the DB row's `ExpiresAt` is now within a few seconds of `now + 24h` — proves sliding fires)
- `Request_WhenSessionAboveRefreshThreshold_DoesNotExtendExpiresAt` (seed `ExpiresAt = now + 20h`, hit `/me`, assert the DB row's `ExpiresAt` is unchanged — proves we don't write on every request)

#### `ListsControllerTests.cs`
- `GetLists_NoToken_Returns401`
- `GetLists_AuthenticatedUser_ReturnsOnlyOwnLists` (Alice creates two lists, Bob creates one; Alice GETs and sees only her two)
- `GetLists_SortedAlphabetically` (create "Zebra", "Apple", "Mango" → returned in A-Z order)
- `CreateList_ValidName_Returns201`
- `CreateList_BlankName_Returns400WithBlankName`
- `CreateList_WhitespaceOnlyName_Returns400WithBlankName`
- `CreateList_DuplicateNameSameUser_Returns409WithDuplicateName` (case-insensitive after trim)
- `CreateList_DuplicateNameDifferentUser_Returns201` (uniqueness is per-user)
- `CreateList_NoToken_Returns401`
- `RenameList_ValidName_Returns200`
- `RenameList_BlankName_Returns400`
- `RenameList_DuplicateName_Returns409`
- `RenameList_OtherUsersList_Returns404` (ownership)
- `RenameList_NonexistentId_Returns404`
- `RenameList_NoToken_Returns401`
- `DeleteList_OwnList_Returns204AndRemovesItems` (cascade: child items gone after delete)
- `DeleteList_OtherUsersList_Returns404`
- `DeleteList_NonexistentId_Returns404`
- `DeleteList_NoToken_Returns401`

#### `ItemsControllerTests.cs`
- `GetItems_OwnList_Returns200WithOpenAndCompletedSplit`
- `GetItems_OpenItemsSortedByOrderThenCreatedAt`
- `GetItems_CompletedItemsSortedByCompletedAtDesc`
- `GetItems_OtherUsersList_Returns404`
- `GetItems_NoToken_Returns401`
- `CreateItem_ValidText_Returns201WithOrderAtEnd` (verify `Order = max(open Order) + 1`)
- `CreateItem_BlankText_Returns400WithBlankText`
- `CreateItem_TextOver500Chars_Returns400WithTextTooLong`
- `CreateItem_OtherUsersList_Returns404`
- `CreateItem_NoToken_Returns401`
- `UpdateItem_EditText_Returns200`
- `UpdateItem_BlankText_Returns400`
- `UpdateItem_TextOver500Chars_Returns400`
- `UpdateItem_CompleteItem_SetsCompletedAtAndKeepsOrder` (mark `completed: true` → `CompletedAt` populated, `Order` unchanged)
- `UpdateItem_UncompleteItem_ClearsCompletedAtAndMovesToBottom` (toggle back to open → `Order = max(open) + 1`, `CompletedAt = null`)
- `UpdateItem_OtherUsersItem_Returns404`
- `UpdateItem_NonexistentId_Returns404`
- `UpdateItem_NoToken_Returns401`
- `DeleteItem_OwnItem_Returns204`
- `DeleteItem_OtherUsersItem_Returns404`
- `DeleteItem_NoToken_Returns401`

#### `MoveItemTests.cs` (added in Phase 2)
- `MoveItem_Top_SetsOrderBelowMin`
- `MoveItem_Bottom_SetsOrderAboveMax`
- `MoveItem_InvalidPosition_Returns400`
- `MoveItem_CompletedItem_Returns409WithItemCompleted`
- `MoveItem_OtherUsersItem_Returns404`
- `MoveItem_NoToken_Returns401`

That's roughly 40 tests at full Phase 2 build-out. They're cheap once the fixture exists — most are 5–10 lines each. Run `dotnet test` and confirm green at every checkpoint.

### 7.2 Frontend tests (optional, last priority)

Add only if all other phases are complete with time to spare. If you do add them, target the modal validation logic first — the rules in §5.3 are the highest-value frontend test surface. Use Vitest + React Testing Library. Skip happy-path render tests.

If skipped, the README's "What I deliberately left out" section should say: "Frontend tests deferred — the highest-risk surface (ownership) is on the backend and is fully covered there; the modal validation rules would be the next priority."

---

## 8. Phased plan with checkpoints

Each phase ends with a hard checkpoint. **Do not move past a checkpoint with a failing or partial item.** If you're out of time at a checkpoint, stop and polish; don't start the next phase.

### Phase 0 — Repo setup

- Add a top-level README stub. (Git was already initialized during §10.1 setup; if not, do `git init` now.)
- `.gitignore`: `bin/`, `obj/`, `node_modules/`, `dist/`, `.vs/`, `.vscode/`, `todo.db`, `todo.db-journal`, `todo.db-wal`, `todo.db-shm`, `*.user`.
- `dotnet new webapi -n TodoApi --use-controllers --framework net10.0` inside `backend/`. Confirm `TodoApi.csproj` shows `<TargetFramework>net10.0</TargetFramework>`. Remove the default WeatherForecast controller and model.
- `npm create vite@latest todo-web -- --template react-ts` inside `frontend/`.
- Verify both projects build/run with `dotnet run` and `npm run dev`. Commit.

**Checkpoint 0:** Empty backend serves at `http://localhost:5000` (or whatever port); empty frontend serves at `http://localhost:5173`. Both repos clean, both committed.

### Phase 1 — Auth + Lists/Items CRUD (the bulk of the work)

**Backend (sub-phase 1a):**
1. Add packages: `Dapper`, `Microsoft.Data.Sqlite`. (`PasswordHasher<T>` ships in the ASP.NET Core shared framework on .NET 8+, so no separate `Microsoft.AspNetCore.Identity` reference is needed — adding it triggers NU1510.)
2. Define POCOs in `Models/` (User, Session, List, Item) — plain classes whose property names match the SQL column names so Dapper's default mapping works without configuration. No DbContext. *(Note: in practice only `User` remained — Session/List/Item POCOs were unused because controllers query directly into in-controller Row records and map to DTOs, so they were removed.)*
3. `Db/SqliteConnectionFactory.cs`: implements `ISqliteConnectionFactory.OpenConnection()`. Opens a `SqliteConnection`, then executes `PRAGMA foreign_keys = ON;` before returning it. Connection string comes from config.
4. `Db/Schema.cs`: static `EnsureCreated(IDbConnection conn)` that executes the SQL from §3.1. Called once at startup against the app DB, and called per-test fixture against the temp DB.
5. `Program.cs`: register `ISqliteConnectionFactory` as a singleton with the SQLite connection string from `appsettings.json` (`Data Source=todo.db`). At startup: open a connection, run `Schema.EnsureCreated`, then check if `Users` is empty — if so, seed Alice and Bob (with PBKDF2 hashes), plus an "Inbox" list and a welcome item per user. Configure CORS to allow `http://localhost:5173`. **Remove the default `app.UseHttpsRedirection()` line** and run HTTP only on `http://localhost:5000` — the frontend will hit HTTP; skipping the dev cert dance saves time.
6. `Auth/PasswordService.cs`: thin wrapper over `PasswordHasher<User>` exposing `Hash(password)` and `Verify(hash, password)`. Registered as singleton.
7. `Auth/SessionAuthHandler.cs`: custom `AuthenticationHandler<AuthenticationSchemeOptions>` that reads `Authorization: Bearer <token>`, queries `Sessions` via Dapper, checks expiry, **applies sliding refresh per §3.3** (one conditional UPDATE when `ExpiresAt - now < 12h`), and on success creates a `ClaimsPrincipal` with `NameIdentifier = UserId`. Define `SessionTtl = 24h` and `RefreshThreshold = 12h` as static readonly fields on the handler. Register as the default auth scheme. Mark controllers `[Authorize]` by default; `[AllowAnonymous]` only on login.
8. `AuthController`: `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`. Login: look up user by email (case-insensitive), verify password, generate token (`Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`), INSERT into `Sessions` with `ExpiresAt = now + SessionTtl` (24h), return `{ token, user }`. Logout: DELETE the session row by token. The sliding refresh logic lives in the auth handler, not here — login only sets the initial expiry.
9. `ListsController`: GET / POST / PATCH /{id} / DELETE /{id}. Every query starts with `WHERE UserId = @currentUserId`. PATCH and DELETE: do a single `UPDATE ... WHERE Id = @id AND UserId = @userId` (or `DELETE ... WHERE ...`) and inspect the affected-row count — 0 rows affected means either not-found or not-owned, both return 404.
10. `ItemsController`: GET /api/lists/{listId}/items, POST /api/lists/{listId}/items, PATCH /api/items/{id}, DELETE /api/items/{id}. For list-scoped routes, validate list ownership first with `SELECT 1 FROM Lists WHERE Id = @listId AND UserId = @userId` (404 if no row). For item-scoped routes, scope via the join `WHERE i.Id = @id AND i.ListId IN (SELECT Id FROM Lists WHERE UserId = @userId)`.
11. DTOs in `Dtos/`. Don't return Dapper-mapped models directly (especially never return `PasswordHash`).
12. Validation: trim and check in the controller method (don't bother with DataAnnotations — the rules are simple enough). On validation failure return the error shape from §4.
13. Wrap any multi-statement write that must be atomic in a Dapper transaction (`conn.BeginTransaction()`, passed via `transaction:`). In practice every item write in `ItemsController` is multi-statement because the parent `Lists.UpdatedAt` is bumped alongside the items change — `CreateItem` (INSERT + UPDATE), `UpdateItem` (optional MAX(Order) read + UPDATE Items + UPDATE Lists), and `DeleteItem` (DELETE + UPDATE) all run inside a transaction so the item change and parent-timestamp bump commit together. Lists handlers are single-row writes and don't need transactions.
14. Smoke test by hand with `curl` or REST Client extension. Verify Alice's token cannot read Bob's list.

**Checkpoint 1a:** All endpoints in §4 (excluding `/move`) work end-to-end via curl. A request without a token returns 401. A request with Alice's token for Bob's list returns 404.

**Backend tests (sub-phase 1b):**
15. Create `TodoApi.Tests` xUnit project (targeting `net10.0`). Reference `TodoApi`. Add `Microsoft.AspNetCore.Mvc.Testing` and `Dapper`.
16. `TestFixture.cs`: a `WebApplicationFactory<Program>` that overrides the `ISqliteConnectionFactory` registration to point at a per-test SQLite file (`Path.GetTempFileName()` + `.db` suffix). On fixture init: run `Schema.EnsureCreated` against that file, then insert Alice and Bob with known hashed passwords. On dispose: delete the file. Exposes helpers `LoginAsAlice() → token`, `LoginAsBob() → token`, `Client(token) → HttpClient`, and `SetSessionExpiry(token, DateTime expiresAt)` for the sliding-expiration tests (writes directly to the Sessions table).
17. Write the per-endpoint tests in §7.1 — every Phase 1 endpoint covered (auth, lists, items). Confirm `dotnet test` is green.

**Checkpoint 1b:** `dotnet test` is green with ~35 tests covering all Phase 1 endpoints — success cases, validation errors, ownership/404s, and 401s.

**Frontend (sub-phase 1c):**
18. `api.ts`: `fetch` wrapper that injects `Authorization: Bearer <token>` from `localStorage`, parses JSON, and rejects in a normalized shape. The shape distinguishes three categories so callers (and the system-error layer) can route correctly:
   - `{ kind: 'request', status, code, message }` — 4xx with a known error code per §4 (validation, 404, 409). Caller handles inline.
   - `{ kind: 'session_expired' }` — 401 on a previously-valid call. The `api.ts` layer handles this itself: clears the token and redirects to `/login?reason=session_expired`. Callers never see it.
   - `{ kind: 'system', mode: 'network' | 'server' }` — fetch threw (`mode: 'network'`) or response was 5xx (`mode: 'server'`). The api client publishes this to a `SystemErrorStore` (in `systemError.ts`); the `ErrorOverlay` component mounts the appropriate modal per §5.6. Callers can also receive it as a rejection if they need to suppress further work.
   - Functions: `login`, `logout`, `me`, `getLists`, `createList`, `renameList`, `deleteList`, `getItems`, `createItem`, `updateItem`, `deleteItem`.
19. `auth.ts`: `getToken()`, `setToken()`, `clearToken()`, `isAuthed()`, `goToLoginWithReason(reason)` (clears token, clears the persisted `activeListId`, sets `window.location` to `/login?reason=<value>`).
20. `systemError.ts`: a tiny pub/sub store (or React context) that holds the current `{ mode: 'network' | 'server' } | null`. `api.ts` publishes; `ErrorOverlay` subscribes via the `useSystemError()` hook.
21. `App.tsx`: simple route guard. If `isAuthed()`, render `AppPage`; else render `LoginPage`. (You can use `react-router-dom` if you want clean URLs — `/login` vs `/`. A small `useState`-based switcher is fine too. Don't overthink it.) Mount the `ErrorOverlay` at the app root so both `LoginPage` and `AppPage` are covered.
22. `LoginPage.tsx`: per §5.1. On mount, read `?reason=session_expired` or `?reason=system_error` from the URL and render the corresponding message in the inline error slot. Handle the `INVALID_CREDENTIALS` error code → inline banner (overrides the reason banner). Banner dismisses on any input change.
23. `AppPage.tsx`: layout with top bar + sidebar + main pane. Top bar includes the user pill with sign-out popover. On mount, rehydrate `activeListId` from `localStorage` (key: `activeListId`); on mobile (`max-width: 768px`) the sidebar is a hidden drawer toggled by a hamburger button in the top bar per §5.5.
24. `Sidebar.tsx`: load lists on mount, render rows, "+" button, active highlighting. Selecting a list updates `activeListId` in `AppPage` state and writes it to `localStorage['activeListId']` so the soft-reload path in §5.6b can rehydrate it.
25. `ListPane.tsx`: header (name + meta + Rename/Delete), pinned `AddItemRow`, open `ItemRow`s, Completed divider, completed `ItemRow`s. Loads items when `activeListId` changes.
26. `ItemRow.tsx`: checkbox toggles completion (NOT optimistic — set the button to a brief disabled/loading state during the request, update UI on success). Click text → inline edit (input replaces text; ↵ saves, Esc cancels). Delete-on-hover (×). All mutations call `api.ts` and update client state only after the response.
27. `ListFormModal.tsx`: per §5.3. Live validation. Reusable for Create and Rename.
28. `DeleteListModal.tsx`: per §5.4.
29. Empty states: main pane when user has no lists → "No lists. Create one with +." No items in selected list → "No items — add one above." Sidebar when user has no lists → "No lists — tap + to create one." (The main-pane empty state is gated on initial fetch completion to avoid flicker. "yet" was intentionally dropped — it reads wrong right after deleting everything.)
30. `ErrorOverlay.tsx`: implements both 5.6a and 5.6b modals (parameterized by mode). Subscribes to the `SystemErrorStore`. Network mode: shows "Can't reach the server" + Retry button; clicking Retry (or the back-off timer firing) calls `me()` and dismisses on success. Server mode: shows "Something went wrong on our end" + OK button; clicking OK runs the soft-reload sequence (`me()` → `getLists()` → `getItems(activeListId)`) and either dismisses or bounces to login per §5.6b.
31. Sign-out wiring: user-menu "Sign out" calls `logout()`, then `clearToken()`, clears `localStorage['activeListId']`, then navigates to `/login` (no reason param — this is a deliberate sign-out, not a forced bounce).

**Checkpoint 1c (Phase 1 complete):**
- Open the app fresh. Sign in as Alice. Create a list. Add three items. Complete one. Edit one. Delete one. Rename the list. Delete the list. Sign out. Sign in as Bob. Confirm Bob does not see Alice's lists. Sign back in as Alice. Confirm her remaining data is intact.
- Close the dev server. Restart it. Confirm data still there.
- Try to create a list with a blank name → see the inline error, Save disabled.
- Try to create a list with a duplicate name → see the inline error, Save disabled.
- Try to add an empty item → Add button disabled.
- Try to type 501 chars in an item → blocked client-side (maxLength=500) or rejected with a server error message shown inline.

**If you need to stop at Checkpoint 1c without doing Phases 2 or 3, that's a complete submission on its own.** Write the README. Submit. Phase 1 alone is a passing submission.

### Phase 2 — Item reorder via Move to top / bottom (only if Phase 1 is solid)

This is the cheapest possible reorder UX that's still a real feature. Skip drag-and-drop entirely.

1. Backend: `POST /api/items/{id}/move` per §4. "top" → set `Order = min(open Order in list) - 1`. "bottom" → set `Order = max(open Order in list) + 1`. Return the updated item.
2. Add `MoveItemTests.cs` per §7.1 (6 tests covering top/bottom success, invalid position, completed→409, ownership→404, no token→401). Run `dotnet test` and confirm green.
3. Frontend: on each open `ItemRow`, add a small grip handle (≡ or ⋮) icon on the left. Click → small popover with two buttons: "Move to top", "Move to bottom". Click one → call API → reorder client list on success.
4. Click outside the popover dismisses it.

**Checkpoint 2:** Move-to-top and move-to-bottom both work. The completed row's checkbox toggle (uncomplete → return to bottom of open list) still works. The Phase 1 manual test script from Checkpoint 1c still passes.

### Phase 3 — Simple search (only if Phase 2 is solid)

Replace the planned ⌘K palette with a simple, finished feature.

1. Frontend only — no new backend endpoints. Search runs client-side over already-loaded data.
2. Add a search input to the top bar between the wordmark and the user pill. Placeholder: "Search lists and items".
3. As the user types (debounced 150ms): match against all loaded list names and all items of all lists. The frontend should `GET /api/lists` plus `GET /api/lists/{id}/items` for every list on app load to populate the search corpus (acceptable for this scale). Refresh the corpus on any mutation.
4. Show a dropdown under the input with up to 10 results. Each row: a tag (`list` or `item`), the matched text, and for items a second line "in <list name>".
5. Click a list result → activate that list in the sidebar, close dropdown, clear search. Click an item result → activate that list, scroll the item into view, close dropdown, clear search. (No flash, no inline-edit-from-search — that's deferred.)
6. Esc clears search and closes the dropdown.

**Checkpoint 3:** Type in the search box; results appear; clicking a list activates it; clicking an item navigates to its list. The Phase 1 + Phase 2 manual test scripts still pass.

---

## 9. README requirements (the submission's README, not this file)

The README is graded. Follow this structure. **Honest beats impressive.**

```markdown
# Todo App — Function Health Take-Home

## What this is
A small full-stack todo app. .NET 10 Web API + Dapper + SQLite on the backend; React + TypeScript + Vite on the frontend.

## How to run
### Prereqs
- .NET 10 SDK
- Node 20+

### Backend
cd backend/TodoApi
dotnet run
# Serves on http://localhost:5000 (see launchSettings.json)
# Creates todo.db in the working directory on first run and seeds two demo users.

### Frontend
cd frontend/todo-web
npm install
npm run dev
# Serves on http://localhost:5173

### Tests
cd backend/TodoApi.Tests
dotnet test

## Demo accounts
- alice@example.com / alice123
- bob@example.com / bob123

## What I built
- Email/password auth with hashed passwords (PBKDF2 via PasswordHasher<T>) and opaque session tokens sent via Authorization: Bearer. Sessions use a 24-hour sliding expiration: every authenticated request extends the session if less than 12 hours remain, using a single conditional UPDATE so most requests don't write.
- Multi-list CRUD with per-user uniqueness, validation (blank, duplicate), and inline error UI.
- Item CRUD with completion state, completed bin, inline edit.
- [If Phase 2 shipped:] Item reordering via "Move to top / Move to bottom" on each open item.
- [If Phase 3 shipped:] Client-side search across list names and item text in the top bar.
- Responsive web layout with a single 768px breakpoint; sidebar becomes a slide-in drawer on narrow screens.
- System-error handling: session expiry bounces to login with an inline reason banner; network failures show a blocking modal with auto-retry; server (5xx) errors show a blocking modal whose OK button refreshes the app's view from the server. No silent failures and no toast-and-undo.
- Backend integration tests covering every endpoint — success cases, validation rejections, ownership enforcement (User A cannot read or write User B's data), and unauthenticated rejection.

## What I deliberately left out
- **Account creation / password reset.** The exercise is graded on the closed loop; demo accounts are sufficient to demonstrate auth + ownership.
- **Drag-and-drop reorder.** Replaced with "Move to top/bottom" to keep the reorder feature finished and tested within scope.
- **⌘K command palette.** Replaced with a simple client-side search box. Keyboard navigation, grouped results, and inline-edit-from-search would expand the UI surface beyond the budget.
- **Sort modes (A-Z / Recent).** Lists are sorted A-Z server-side. The user-facing toggle adds state surface for marginal value.
- **Separate mobile patterns** (bottom sheets, full-screen mobile search sheet). The single responsive layout works on phones; bespoke mobile patterns would roughly double the UI work.
- **Real-time sync / multi-tab consistency.** The frontend assumes it's the only writer per tab. Editing in two tabs at once will produce last-write-wins behavior with no live updates.
- **Soft delete / undo.** Deletes are permanent — matching the wireframes' "This can't be undone" copy.
- **Frontend tests.** The highest-risk surface (ownership) is on the backend and is covered by integration tests there.

## What I'd do with another day
- Drag-and-drop reorder (HTML5 DnD or `@dnd-kit/core`) with a backend endpoint that accepts a full ordered list of item IDs in one request, so reorders are atomic.
- The full ⌘K palette with grouped, keyboard-navigable results and substring highlighting.
- Account creation + password reset.
- An absolute session lifetime cap (e.g., `CreatedAt + 30d`) on top of the sliding 24-hour TTL, so a stolen token can't be kept alive forever by an attacker who pings the API once every 12 hours. Skipped here to keep the auth path to one column comparison.
- A draft-restore mechanism for in-flight typed input. The current system-error modals interrupt the user, and any text being typed in an Add-item input or a rename modal is lost on recovery. A small `localStorage`-backed draft store keyed by form ID would restore it. Skipped here because the modals are uncommon by design.
- Optimistic UI updates (e.g., via React Query) so mutations feel instant. Deliberately skipped here because pairing optimistic updates with the system-error modals would require a "remember what to revert" layer; the current "brief disabled state, update on response" pattern stays correct under all failure modes.
- Frontend component tests for the modals (the §5.3 validation rules are the highest-value test target).
- A small CI workflow (GitHub Actions) running `dotnet test` and `npm run build` on PR.

## Assumptions
- Two seeded users is sufficient demonstration of multi-tenant ownership for this exercise.
- File-based SQLite is acceptable (the assignment lists "SQL Lite or EF Core in memory" — chose file-backed SQLite so data survives restart, with Dapper as the data-access layer instead of EF Core).
- Session tokens are opaque random strings stored in a `Sessions` table (chosen over JWT so logout truly invalidates and so there is no signing-key story to explain — both meaningful for a small app).
- Deletes are permanent; the wireframes' "This can't be undone" copy matches.

## Architecture notes
- One backend project. Controllers call Dapper directly via an injected `ISqliteConnectionFactory`. No repository layer wrapping Dapper (Dapper is already thin enough). No CQRS/MediatR. This shape is deliberate — see the prep guide on over-engineering.
- The schema is defined in one place (`Db/Schema.cs`) as raw SQL and applied idempotently at startup and per test fixture. No migration framework — the app has one schema version.
- SQLite foreign keys are explicitly enabled per connection (`PRAGMA foreign_keys = ON`); this is required for `ON DELETE CASCADE` to fire.
- Errors use a single consistent shape ({ error: { code, message } }); the frontend keys off `code` for known cases.
- Ownership is enforced at every query: each query starts by filtering on the authenticated user's ID. There is no place in the code where this filter can be bypassed — verified by the ownership tests.
```

---

## 10. Handoff to Claude Code

Claude Code sessions don't share context with each other. Pasting BUILD_PLAN.md into one session does not carry over to the next. The reliable pattern is to put the planning artifacts in the repo and let Claude Code's `CLAUDE.md` auto-load behavior bootstrap every session.

### 10.1 One-time repo setup (before the first Claude Code session)

Create the repo directory and copy these files in:

```
repo/
├── CLAUDE.md                     # auto-loaded by Claude Code every session
├── docs/
│   ├── BUILD_PLAN.md             # this file
│   └── design-handoff.md         # the design README, copied from Downloads
└── (backend/, frontend/, README.md will be created during Phase 0)
```

Concretely:

1. `mkdir` the repo folder and `cd` into it.
2. From the planning folder (`C:\Users\flint\Documents\Claude\Projects\Function health take home programming assignment\`), copy `CLAUDE.md` into the repo root, and the entire `docs/` folder (which contains `BUILD_PLAN.md` and `design-handoff.md`) into the repo.
3. `git init`. Don't commit yet — Phase 0's `.gitignore` step happens first.

The two assignment PDFs do **not** go in the repo. The brief's requirements are already absorbed into BUILD_PLAN.md. The prep guide is Function Health's internal document — its guidance is captured in BUILD_PLAN.md §1.

### 10.2 Starting a Claude Code session

Run `claude` in the repo root. Claude Code will auto-load `CLAUDE.md`, which points it to `docs/BUILD_PLAN.md` and instructs it to maintain `docs/claude_build_tracking.md` (see CLAUDE.md "Build progress tracking" section).

**Very first session in the repo** — kickoff prompt:

> Read `docs/BUILD_PLAN.md` and `docs/design-handoff.md` end-to-end. Then follow the "Build progress tracking" instructions in `CLAUDE.md` — generate `docs/claude_build_tracking.md` with the full numbered step list and wait. Do not write any other code yet.

After reviewing the step list, drive the build with:

> Go through step N.

Claude Code will execute steps from the current state up to and including step N, mark each `[x]` in the tracking file, then stop. To revise the plan before continuing: `Before continuing, change step 6 to ...`.

**Follow-up session** (new conversation, same repo) — short kickoff:

> Read `docs/claude_build_tracking.md`, tell me what's done and what the next step is. Wait for me to tell you what to do.

**Resuming an in-progress conversation** — use `claude --resume` instead of starting fresh. The conversation history persists and you can continue without re-bootstrapping.

### 10.3 Before submission — what to do with the planning artifacts

The prep guide says "check your repository for anything that should not be there." Decide deliberately about each of these non-product files; both directions are defensible:

- **`CLAUDE.md`** — small, clearly labeled, harmless to leave. Reviewers familiar with Claude Code will recognize it; those who aren't will see a short markdown file pointing at BUILD_PLAN.md.
- **`BUILD_PLAN.md`** — leaving it in signals transparency about scope reasoning (which is itself what the prep guide is grading on); removing it signals "the product speaks for itself." Lean toward leaving it if the README references it; lean toward removing it if the README is fully self-contained.
- **`docs/design-handoff.md`** — leaving it shows the design context the build was working from. Reasonable to keep.
- **`claude_build_tracking.md`** — operational log of what got built, skipped, and in what order. Useful as evidence-of-process if you keep it; safe to remove if you want a tighter repo. If you keep it, the `[~] skipped` lines and their reasons are actually a complement to the README's "what I deliberately left out" section.

If you keep BUILD_PLAN.md, consider adding a one-line note in the submission README pointing the reviewer at it ("Scope reasoning, phased plan, and explicit non-goals are documented in `BUILD_PLAN.md` for transparency."). That turns the artifact into a feature rather than clutter.

What should definitely not be there: build outputs (`bin/`, `obj/`, `node_modules/`, `dist/`), the SQLite database file (`todo.db`), and any IDE state (`.vs/`, `.vscode/`) — all already in the gitignore from Phase 0.
