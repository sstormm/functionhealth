import type { ItemsResponse, ListSummary } from '../types';
import { AddItemRow } from './AddItemRow';
import { ItemRow } from './ItemRow';

type Props = {
  list: ListSummary;
  items: ItemsResponse | null;
  loading: boolean;
  onRename: () => void;
  onDelete: () => void;
  onChanged: () => void | Promise<void>;
};

export function ListPane({ list, items, loading, onRename, onDelete, onChanged }: Props) {
  return (
    <>
      <div className="list-header">
        <div className="title-row">
          <h2>{list.name}</h2>
          <span className="meta">
            {list.openCount} open · {list.doneCount} done
          </span>
        </div>
        <div className="actions">
          <button type="button" className="btn" onClick={onRename}>
            Rename
          </button>
          <button type="button" className="btn" onClick={onDelete}>
            Delete list
          </button>
        </div>
      </div>
      <div className="items-region">
        <AddItemRow listId={list.id} onAdded={onChanged} />
        {!items && loading ? null : items ? (
          <>
            {items.open.length === 0 ? (
              <div className="items-empty">No items — add one above.</div>
            ) : (
              <div className="items-card">
                {items.open.map((item) => (
                  <ItemRow key={item.id} item={item} onChanged={onChanged} />
                ))}
              </div>
            )}
            <div className="completed-divider">Completed · {items.completed.length}</div>
            {items.completed.length > 0 && (
              <div className="items-card">
                {items.completed.map((item) => (
                  <ItemRow key={item.id} item={item} onChanged={onChanged} />
                ))}
              </div>
            )}
          </>
        ) : null}
      </div>
    </>
  );
}
