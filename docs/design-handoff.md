# Handoff: Todo App

## Overview

A web + mobile todo application with:
- Login (email/password) with inline-error failure state, and a sign-out affordance from inside the app
- Multiple to-do **lists**, each containing **items**
- A persistent left **sidebar** of lists; selected list shows its items in the main pane
- Items are single-string todos with a checkbox; checked items move to a **Completed bin** at the bottom of the active list
- Item reordering via drag, plus a quick "Move to top / bottom" menu opened by clicking the grip
- List **create**, **rename**, and **delete** with modals + inline error states
- Cross-app **search** (⌘K command palette) that returns matched list names and matched item text — clicking a result navigates to the list and (for items) enters inline edit mode on the matched row

## About the design files

The HTML files in this bundle are **design references** — low-fidelity wireframes built in HTML for layout and interaction documentation only. They are not production code to copy. **Recreate these designs in the target codebase's environment.** The product is intended to be **React**.

Use the wireframes as the source of truth for:
- Information architecture (what lives where on each screen)
- Interaction model (what clicks/drags/hotkeys do)
- States and transitions (e.g. typing → results panel; clicking item → flash → edit mode)

Apply the **existing or chosen design system** for actual visual styling. The mocks use a "Bootstrap-ish" baseline aesthetic — clean, functional, minimal. No fancy gadgets, no heavy theming required.

## Fidelity

**Low-fidelity wireframes.** Layout and structure are intentional and should be matched. Final colors, exact typography, spacing tokens, and component visual styles should come from your codebase's design system (Bootstrap, Tailwind, MUI, custom — whatever is in place). The wireframes use a flat off-white background, charcoal borders, IBM Plex Sans for UI, Caveat for the orange annotations (which are *commentary only* — don't recreate the annotations).

## Tech assumptions

- **Frontend**: React (per user)
- **Backend**: Out of scope here — assume a typical CRUD API with endpoints for lists and items
- **Persistence**: lists, items, sort order, completion state, and recently-modified timestamps (needed for the "Recent" sort)
- **Auth**: standard email/password with an error response that triggers the inline failure UI; client-side sign-out clears session and returns user to the login screen

---

## Data model

```
User
  - email
  - passwordHash
  - …

List
  - id
  - userId
  - name                    (editable; renamable from the list header; must be unique per user and non-blank)
  - createdAt
  - updatedAt               (used for "Recent" sort; bumped on any change inside the list)

Item
  - id
  - listId
  - text                    (single string)
  - completed: boolean
  - order: integer          (manual order within its list; open items only)
  - completedAt: timestamp  (optional, for sorting completed bin)
  - createdAt
  - updatedAt
```

Notes:
- Open items maintain a manual `order`. Completed items are not user-orderable (see the rules below).
- "Recent" sort for **lists** = sort by `updatedAt` descending (most recent activity first).
- Search runs across the user's lists (by `name`) and items (by `text`).
- List names must be **non-blank** and **unique** per user. The UI surfaces both errors inline (see the Rename / New list modal section).

---

## Screens / views

### 1 · Login (failure)

**File**: `Todo App Wireframes.html` → section "Authentication"

**Purpose**: User signs in. On bad credentials, show the failure state.

**Layout**:
- Centered card on a neutral background, max width ~340px
- Card contents top → bottom:
  - Heading: "Sign in"
  - Sub-copy: "Welcome back to todo."
  - Email input (labeled "Email")
  - Password input (labeled "Password")
  - **Inline error banner** (red background, red border) — sits ABOVE the Sign in button, not as a toast
  - Primary "Sign in" button (full width)
  - Secondary links row: "Forgot password? · Create account"

**Failure-state details**:
- Password input gets red border + red halo (focus ring)
- Error banner copy: "That email & password don't match. Try again."
- No toast or alert; error sits inline so user can correct without losing context
- On retry, banner clears as user types

**Mobile**: same form, full-width on the device with edge padding (~18px). Same components, smaller heading.

---

### 2 · Sidebar + items (the core flow)

**File**: `Todo App Wireframes.html` → section "Sidebar + items · the core flow"

**Purpose**: The main app surface after login.

**Layout**:
- App chrome:
  - **Top bar** (full width, ~42px tall): logo/wordmark "todo" on the left; **Search** button (icon + word + ⌘K kbd badge); **User pill** on the far right (avatar circle + email + ▾)
  - **Left sidebar** (~200px wide, full-height): "Lists" header with sort button + "+" new-list button, then a vertical list of the user's lists
  - **Main pane** (rest of the width): selected list's items

#### 2a · User menu (sign-out)
- The user pill in the top-right of the top bar is a dropdown trigger.
- Click → small popover with:
  - Small muted label: `Signed in as` + the user's email (bold)
  - **Sign out** (red text, only menu item)
- This is the ONLY way to sign out from the web. Keep it minimal — no account settings, no profile menu beyond this.

#### 2b · Sidebar (lists nav)

- Header row:
  - Label "LISTS" (small caps, uppercase, muted)
  - **Sort button**: small pill showing the current sort (default "A–Z ▾"); clicking opens a dropdown with two options: **A–Z** (default, selected) and **Recent** (sorts by most-recent activity, descending). Selected option marked with a check.
  - **+ new list** button → opens the New list modal (see §3)
- List rows: each row is `{ name … count }`. The active list has an orange-tinted background + orange left-border accent.
- The sidebar list area **scrolls independently** when there are more lists than fit vertically. A thin scroll indicator (track + thumb) on the right edge tells the user there's more.

#### 2c · Main pane (active list)

Top to bottom:
1. **List title** (h2) + meta ("N open · M done") + right-aligned **Rename** / **Delete list** buttons.
   - **Rename** → opens the Rename list modal (see §3)
   - **Delete list** → opens the Delete-confirm modal (see §4)
2. **Add-item row**: text input with placeholder "Add an item…" + primary "Add" button. **Stays pinned** at the top of the pane when items scroll. ↵ in the input adds the item.
3. **Open items** card: list of item rows, each containing:
   - **Grip handle** (dotted pattern, draggable). Single-click → opens a small popover with "Move to top" / "Move to bottom". Click-and-drag → starts free reorder.
   - **Checkbox** (square; ✓ on check)
   - **Item text** (single-line)
4. A horizontal dashed divider labeled **"Completed · N"** (always visible — **not collapsible**).
5. **Completed items** card: list of completed item rows:
   - **No grip** (completed items are not draggable / not reorderable)
   - **Checkbox** (checked state, can be un-checked → row moves back to the open list above)
   - **Item text** with `text-decoration: line-through` and muted color
6. The items area (open card + completed bin) **scrolls independently** of the rest of the page when long. The add-item row stays pinned; the list title row also stays at the top.

---

### 3 · Create / Rename a list (modals)

Both Create and Rename use the **same modal component** with the same validation rules. Centered card over a dimmed backdrop.

**Layout**:
- Title: "New list" (for create) or "Rename list" (for rename)
- Single labeled text input ("List name")
  - For Rename: prefilled with the current list name; cursor at end; text selected on open
  - For Create: empty with placeholder (e.g. "e.g. Reading"); cursor in input
- **Inline error slot** under the input (red border on the input + red error message below)
- Action row: **Cancel** (secondary) + **Save** (rename) / **Create list** (new) on the right
- ↵ submits, Esc cancels

**Validation rules** (live, on every keystroke):

| Condition | Error message | Save state |
|---|---|---|
| Input is blank | `List name can't be blank.` | Disabled |
| Input matches an existing list (case-insensitive, trimmed) | `A list named "<name>" already exists.` | Disabled |
| For Rename: input matches the original (no change) | (no error, but Save is a no-op — disable or close on submit) | Disabled |
| Else | — | Enabled |

The Save / Create button visibly disables (muted background, lower opacity) whenever an error is showing.

**On success**:
- Rename: modal closes, list header updates, sidebar entry updates and remains active
- Create: modal closes, new empty list is appended to the sidebar AND becomes the active list (jump straight into it so the user can start adding items)

---

### 4 · Deleting a list

- Trigger: "Delete list" button in the list header.
- Modal opens centered, dims the rest of the page.
- Copy: `Delete "<List name>"?` + `N open items and any completed items will be permanently removed. This can't be undone.`
- Buttons (right-aligned): **Cancel** (secondary) + **Delete list** (destructive red).
- After delete: modal closes, sidebar selection moves to the next sensible list (suggest: Inbox if it exists, else the first list).

---

### 5 · Item reordering (open items only)

Two ways to reorder, both on the grip handle of an **open** item:

1. **Single click on grip** → small popover near the grip with two options:
   - "↑ Move to top"
   - "↓ Move to bottom"

2. **Click and drag the grip** → free drag-reorder. While dragging:
   - Picked-up row gets an orange outline and a slight lift (translateY)
   - Other rows slide to make room as the cursor moves
   - Drop indicator preview between rows
   - Release → server save

**Completed items have no grip** and cannot be reordered. The sort within the Completed bin is fixed (suggest: by `completedAt` desc — newest first).

---

### 6 · Search · ⌘K command palette

#### Trigger
- **Search button** in the top bar (icon + label "Search" + ⌘K kbd badge) — always visible
- Hotkey **⌘K** (or Ctrl+K on non-Mac) opens the palette from anywhere

#### Palette
- Centered overlay, max width ~460px, top-anchored about 50px from the top of the viewport.
- Dark veil over the app behind (~28% black).
- Top: a single search input with placeholder and an ESC kbd badge.
- Below the input: a results body (scrolls independently when long).
- Footer: keyboard hint "↑↓ navigate · ↵ open · esc close" on the left; result count on the right.

#### Results grouping
Two groups, in order:

1. **Lists · N** — list-name matches. Group header has a chevron (▾) and is **collapsible** (click header to toggle ▸/▾). When collapsed, the items group gets the full available room.
2. **Items · N** — item-text matches. Group header has an **"Include completed"** checkbox on the right (off by default). When on, completed-item matches are appended below the open-item matches with `line-through` text and slightly muted color. The Items group is **not** collapsible.

Each result row:
- A small monospace tag on the left: `list` or `item`
- The matched text with the query substring highlighted in `<mark>` style (yellow background)
- For item results, a second line shows "in <List name>"

The first matching list (or item, if no list matches) is auto-selected on open and ↑↓-navigable. ↵ opens whatever is selected.

---

### 7 · Search outcomes

The palette dismisses on every selection. Behavior depends on what was clicked:

#### 7a · Clicked a **list** result
- Sidebar selection moves to that list (active state on the sidebar row).
- Main pane loads that list's items.
- No item is highlighted.

#### 7b · Clicked an **open item** result
- The item's parent list loads in the main pane.
- The clicked row gets a brief **flash** (~300ms — soft orange background that fades to normal).
- Row enters **inline edit mode**: text becomes editable, cursor placed **at the end** of the existing text. ↵ saves, Esc cancels. No matched-text highlight (the substring is *not* yellow once the row is opened).
- If the row would be off-screen, scroll it into view first.

#### 7c · Clicked a **completed item** result
- Same as 7b but the item lives in the Completed bin.
- Bin stays visible (it always is — never collapsed).
- Flash + edit-mode-with-caret-at-end happens on the completed row.

---

### 8 · Long-list handling

Three independently-scrollable regions, each with a thin gray scroll indicator (track + thumb):

1. **Sidebar** — when the user has more lists than fit. Add-list "+" button and sort button stay pinned at the top.
2. **Items pane** — when the active list has many items. The list title row + add-item row stay pinned. Completed bin still anchors the bottom of the scrollable content.
3. **Palette results** — when there are many matches. The search input + footer (keyboard hints) stay pinned; the body between them scrolls.

---

### 9 · Mobile

The same patterns translate to a single-column mobile layout.

#### 9a · Login
- Same form as desktop, full-width with edge padding. Same failure-state error banner.

#### 9b · Lists view (the mobile "home")
- Header (left → right): page title "Lists" · centered ⌕ search icon (absolutely centered) · right cluster of `A–Z ▾` sort pill + circular avatar/profile button
- Tap the sort pill → small popover with A–Z (selected) and Recent
- Tap the **avatar** → **bottom sheet** with: muted label `Signed in as <email>` and **Sign out** (red). This is the mobile sign-out path.
- Below header: vertical list of `{ name … count … chevron > }`. Tap a row → list-detail view.
- The lists area scrolls independently with the same thin gray indicator.

#### 9c · List-detail view
- Header (left → right): `‹` back chevron + list name · **centered ⌕ search icon** · `⋯` more menu on the right
- Tap **⋯** → **bottom action sheet** with: muted label of the list name, **Rename list** (with edit icon), **Delete list** (red, with trash icon), and **Cancel** at the bottom (separated)
- Below header: pinned add-item row, then open items, then the Completed bin (always visible)
- Items use the same grip behaviour as desktop: tap grip → Top/Bottom popover; long-press-and-drag → free reorder; completed items have no grip

#### 9d · Rename / New list modals (mobile)
- Same content & validation rules as desktop, presented as a centered card (~14px from each edge, top-anchored ~60px from top) over a dimmed Lists-view backdrop
- Same error states: blank name (`List name can't be blank.`) and duplicate (`A list named "<name>" already exists.`)
- Save / Create button visibly disables on error

#### 9e · Mobile search
- Tapping the ⌕ icon pushes a **full-screen search sheet**: `‹ [input]` in the header, then result groups (Lists, Items) below.
- Lists group is still collapsible; Items group still has the Include-completed toggle.

---

## Design tokens (low-fi reference values — replace with your system)

| Token | Wireframe value | Notes |
|---|---|---|
| App background | `#faf9f6` (warm off-white) | Or your system surface |
| Panel background | `#ffffff` | Cards, sidebar, top bar |
| Ink (text) | `#1a1a1a` | Primary text |
| Ink muted | `#444` / `#7a7a7a` | Secondary text |
| Accent (active/highlight) | `#c2410c` (warm orange) | Active list, flash, focus rings, avatar fill |
| Accent soft | `#fde7dc` | Active-row tint, flash residual |
| Soft border | `#c8c4bd` | Dividers |
| Danger | `#b91c1c` | Delete button, error state, Sign out |
| Danger surface | `#fee2e2` | Error banner background |
| Highlight (mark) | `#fde047` | Search match substring |
| Completed text | `#7a7a7a` + line-through | Completed items |
| Border radius | 3–4px | Bootstrap-ish, not pillowy |
| Font, UI | system sans / IBM Plex Sans | Whatever your codebase uses |
| Font, monospace | system mono | For counts, kbd badges, tags |

---

## Interactions summary (quick reference)

| Action | Result |
|---|---|
| Sidebar row click | Make that list active; main pane loads its items |
| Sidebar sort pill | Open popover; A–Z (default) or Recent |
| Sidebar **+ button** | Open **New list** modal |
| List header **Rename** | Open **Rename list** modal (prefilled, cursor at end) |
| List header **Delete list** | Open **delete-confirm** modal |
| User pill (top bar) | Open user menu (only action: **Sign out**) |
| Add-item input ↵ or Add button | Append item to the open items |
| Click a checkbox (open item) | Mark complete → row moves to Completed bin |
| Click a checkbox (completed item) | Un-complete → row moves back to open items |
| Single-click an open item's grip | Open Top/Bottom popover |
| Drag an open item's grip | Free reorder |
| Click Search button or ⌘K | Open palette |
| Type in palette | Live filter; Lists group + Items group update |
| Click ▾ Lists header | Toggle Lists group collapse |
| Toggle "Include completed" | Append completed-item matches |
| Click any result | Dismiss palette + navigate (see Search outcomes) |
| Esc in palette | Dismiss without navigating |
| Mobile: tap avatar | Open bottom sheet with **Sign out** |
| Mobile: tap ⋯ on list-detail | Open bottom sheet with **Rename list** / **Delete list** |
| Submit Rename/New list with blank | Inline error; Save disabled |
| Submit Rename/New list with duplicate | Inline error; Save disabled |

---

## State management

Minimum state to track in the client (assuming server is source of truth):

- `currentUser`
- `lists[]` (with their `updatedAt` for sort)
- `activeListId`
- `items[]` for the active list (open + completed, split client-side or by server)
- `listsSort: 'a-z' | 'recent'` (persist in user prefs)
- Search:
  - `searchOpen: boolean`
  - `query: string`
  - `listMatches[]`, `itemMatches[]`, `completedItemMatches[]`
  - `listsCollapsed: boolean`
  - `includeCompleted: boolean`
- Modal state:
  - `newListModalOpen: boolean`
  - `renameListModal: { open: boolean, listId?: string }`
  - `deleteListModal: { open: boolean, listId?: string }`
  - For each modal: the current input value + any computed error
- Edit-mode-from-search: `pendingEditItemId` — when set, after the list loads, the row enters edit mode with caret at end. Clear on save/cancel.
- Reorder: transient drag state if you implement drag-and-drop

---

## Files in this bundle

| File | Purpose |
|---|---|
| `Todo App Wireframes.html` | Main wireframe document. Open in a browser to interact with the design canvas. |
| `wireframe-kit.jsx` | Low-fi component primitives (placeholders, annotations, scroll indicators) — **reference only**, do not ship |
| `direction-1-search.jsx` | All the screen mocks as React components — **reference only** |
| `design-canvas.jsx` | Canvas chrome (pan/zoom, artboard frames) — **NOT product code**, this is the design-tool wrapper |
| `tweaks-panel.jsx` | Tweaks UI for the wireframe tool — **NOT product code** |

To view the wireframes: open `Todo App Wireframes.html` in any modern browser. Pan with click-drag, zoom with scroll. Use the ⋯ menu on each artboard to focus it fullscreen.

Annotations (handwritten orange text and arrows) are commentary aimed at developers — they describe interactions and are **not part of the UI to implement**.

---

## Open questions for the implementer

If anything's ambiguous, ask the design owner. Likely questions:

- **Completed bin sort**: most-recent-completed first (suggested), or insertion order?
- **Search debouncing**: 100–150ms is a sensible default.
- **Mobile keyboard handling** in search and add-item: keep the input above the keyboard.
- **Empty states**: not designed in this pass — show simple placeholder copy in each pane (e.g. "No items yet — add one above" or "No lists yet — tap + to create one").
- **Real-time sync vs. optimistic UI**: pick whichever fits the backend.
- **Soft delete vs. hard delete** for lists / items: not specified — wireframes describe it as permanent ("This can't be undone"). Confirm before implementing.
