import type { ListSummary } from '../types';

type Props = {
  lists: ListSummary[];
  activeListId: string | null;
  onSelect: (id: string) => void;
  onCreate: () => void;
  isOpen?: boolean;
  onClose?: () => void;
};

export function Sidebar({ lists, activeListId, onSelect, onCreate, isOpen, onClose }: Props) {
  return (
    <>
      {isOpen && <div className="sidebar-overlay" onClick={onClose} />}
      <aside className={`sidebar${isOpen ? ' open' : ''}`}>
        <div className="sidebar-header">
          <span className="label">Lists</span>
          <button
            type="button"
            className="icon-btn"
            onClick={onCreate}
            title="New list"
            aria-label="New list"
          >
            +
          </button>
        </div>
        <div className="sidebar-list">
          {lists.length === 0 ? (
            <div className="sidebar-empty">No lists — tap + to create one.</div>
          ) : (
            lists.map((l) => (
              <button
                key={l.id}
                type="button"
                className={`sidebar-row${l.id === activeListId ? ' active' : ''}`}
                onClick={() => onSelect(l.id)}
              >
                <span>{l.name}</span>
                <span className="count">{l.openCount}</span>
              </button>
            ))
          )}
        </div>
      </aside>
    </>
  );
}
