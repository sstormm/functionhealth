import { useEffect, useState } from 'react';
import { getItems, getLists, logout, me } from '../api';
import {
  clearActiveListId,
  getActiveListId,
  goToLogin,
  setActiveListId,
} from '../auth';
import { DeleteListModal } from '../components/DeleteListModal';
import { ListFormModal, type ListFormMode } from '../components/ListFormModal';
import { ListPane } from '../components/ListPane';
import { Sidebar } from '../components/Sidebar';
import { UserMenu } from '../components/UserMenu';
import type { ApiFailure, ItemsResponse, ListSummary, User } from '../types';

export function AppPage() {
  const [user, setUser] = useState<User | null>(null);
  const [lists, setLists] = useState<ListSummary[]>([]);
  const [activeListId, setActiveListIdState] = useState<string | null>(getActiveListId());
  const [items, setItems] = useState<ItemsResponse | null>(null);
  const [loadingItems, setLoadingItems] = useState(false);

  const [listForm, setListForm] = useState<ListFormMode | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ListSummary | null>(null);

  const [drawerOpen, setDrawerOpen] = useState(false);

  // Centralize active-list changes so stale items always clear in lockstep
  // with the id — keeps the loading effect free of synchronous setState.
  const setActive = (id: string | null) => {
    setActiveListIdState(id);
    setItems(null);
    if (id) setActiveListId(id);
    else clearActiveListId();
  };

  // Initial load
  useEffect(() => {
    (async () => {
      try {
        const [u, ls] = await Promise.all([
          me({ publishSystemErrors: false }),
          getLists({ publishSystemErrors: false }),
        ]);
        setUser(u);
        setLists(ls);
        const stored = getActiveListId();
        const storedValid = stored !== null && ls.some((l) => l.id === stored);
        if (storedValid) {
          setActiveListIdState(stored!);
        } else if (ls.length > 0) {
          // No valid stored selection — auto-select the first list (A-Z)
          // so the app opens populated rather than to the empty state.
          setActive(ls[0].id);
        } else if (stored) {
          // Had a stale ID and no fallback list to pick.
          setActive(null);
        }
      } catch (err) {
        const f = err as ApiFailure;
        if (f.kind === 'system') {
          // Backend unreachable on initial load — no meaningful UI to render,
          // so bounce to login. Suppressed the system-error modal above so it
          // doesn't flash before the redirect.
          goToLogin();
        }
        // 401 is handled by api.ts (silent redirect); 'request' shouldn't reach here.
      }
    })();
  }, []);

  // Load items when the active list changes. Gated on `user` so the initial
  // mount doesn't race the user/lists fetch — if that fetch fails and we
  // redirect to login, this effect never fires its (also-failing) request.
  useEffect(() => {
    if (!user) return;
    if (!activeListId) return;
    let cancelled = false;
    (async () => {
      setLoadingItems(true);
      try {
        const result = await getItems(activeListId);
        if (!cancelled) setItems(result);
      } catch (err) {
        if (cancelled) return;
        const f = err as ApiFailure;
        if (f.kind === 'request' && f.code === 'NOT_FOUND') {
          // list disappeared between load and fetch — clear active selection
          setActive(null);
        }
      } finally {
        if (!cancelled) setLoadingItems(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [activeListId, user]);

  const refreshLists = async (): Promise<ListSummary[]> => {
    const ls = await getLists();
    setLists(ls);
    return ls;
  };

  const refreshItems = async () => {
    if (!activeListId) return;
    try {
      const result = await getItems(activeListId);
      setItems(result);
    } catch {
      // ignore — overlay handles system errors
    }
  };

  const refreshBoth = async () => {
    await Promise.all([refreshLists(), refreshItems()]);
  };

  const selectList = (id: string) => {
    // Clicking the already-active list is a no-op (other than closing the
    // mobile drawer). setActive() clears items unconditionally, which would
    // wipe the items view since the [activeListId] effect doesn't re-fire
    // when the id didn't change.
    if (id !== activeListId) setActive(id);
    setDrawerOpen(false);
  };

  const onListCreated = (list: ListSummary) => {
    selectList(list.id);
    void refreshLists();
  };

  const onListRenamed = () => {
    void refreshLists();
  };

  const onListDeleted = async () => {
    const deletedId = deleteTarget?.id;
    if (!deletedId) return;
    const wasActive = activeListId === deletedId;
    if (wasActive) setActive(null);
    const ls = await refreshLists();
    if (wasActive && ls[0]) setActive(ls[0].id);
  };

  const onSignOut = async () => {
    await logout();
    clearActiveListId();
    window.location.href = '/login';
  };

  const activeList = lists.find((l) => l.id === activeListId) ?? null;
  const existingNamesLower = lists.map((l) => l.name.trim().toLowerCase());

  return (
    <div className="app-shell">
      <header className="top-bar">
        <button
          type="button"
          className="hamburger"
          onClick={() => setDrawerOpen((v) => !v)}
          aria-label="Toggle list drawer"
        >
          ≡
        </button>
        <span className="wordmark">todo</span>
        <div className="top-bar-spacer" />
        {user && <UserMenu user={user} onSignOut={onSignOut} />}
      </header>

      <div className="app-body">
        <Sidebar
          lists={lists}
          activeListId={activeListId}
          onSelect={selectList}
          onCreate={() => setListForm({ kind: 'create' })}
          isOpen={drawerOpen}
          onClose={() => setDrawerOpen(false)}
        />

        <main className="main-pane">
          {activeList ? (
            <ListPane
              list={activeList}
              items={items}
              loading={loadingItems}
              onRename={() =>
                setListForm({ kind: 'rename', listId: activeList.id, currentName: activeList.name })
              }
              onDelete={() => setDeleteTarget(activeList)}
              onChanged={refreshBoth}
            />
          ) : user ? (
            // Reachable only after initial fetch completes and the user truly
            // has zero lists — the `user` gate suppresses a flicker of this
            // copy during the brief moment before me() + getLists() resolve.
            <div className="main-empty">No lists. Create one with +.</div>
          ) : null}
        </main>
      </div>

      {listForm && (
        <ListFormModal
          mode={listForm}
          existingNamesLower={existingNamesLower}
          onClose={() => setListForm(null)}
          onCreated={onListCreated}
          onRenamed={onListRenamed}
        />
      )}

      {deleteTarget && (
        <DeleteListModal
          list={deleteTarget}
          onClose={() => setDeleteTarget(null)}
          onDeleted={onListDeleted}
        />
      )}
    </div>
  );
}
