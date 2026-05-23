import { useEffect, useRef, useState, type KeyboardEvent } from 'react';
import { deleteItem, updateItem } from '../api';
import type { ApiFailure, Item } from '../types';

type Props = {
  item: Item;
  onChanged: () => void | Promise<void>;
};

export function ItemRow({ item, onChanged }: Props) {
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editValue, setEditValue] = useState(item.text);
  const [rowError, setRowError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (editing) {
      inputRef.current?.focus();
      inputRef.current?.select();
    }
  }, [editing]);

  const startEdit = () => {
    if (item.completed) return;
    setEditValue(item.text);
    setRowError(null);
    setEditing(true);
  };

  const cancelEdit = () => {
    setEditing(false);
    setEditValue(item.text);
    setRowError(null);
  };

  const saveEdit = async () => {
    const trimmed = editValue.trim();
    if (trimmed === '') {
      setRowError("Item text can't be blank.");
      return;
    }
    if (trimmed === item.text) {
      setEditing(false);
      return;
    }
    setBusy(true);
    try {
      await updateItem(item.id, { text: trimmed });
      setEditing(false);
      await onChanged();
    } catch (err) {
      const f = err as ApiFailure;
      if (f.kind === 'request') setRowError(f.message);
    } finally {
      setBusy(false);
    }
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      void saveEdit();
    } else if (e.key === 'Escape') {
      e.preventDefault();
      cancelEdit();
    }
  };

  const toggleComplete = async () => {
    if (busy) return;
    setBusy(true);
    try {
      await updateItem(item.id, { completed: !item.completed });
      await onChanged();
    } catch (err) {
      const f = err as ApiFailure;
      if (f.kind === 'request') setRowError(f.message);
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (busy) return;
    setBusy(true);
    try {
      await deleteItem(item.id);
      await onChanged();
    } catch (err) {
      const f = err as ApiFailure;
      if (f.kind === 'request') setRowError(f.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className={`item-row${item.completed ? ' completed' : ''}`}>
      <button
        type="button"
        className={`checkbox${item.completed ? ' checked' : ''}`}
        onClick={toggleComplete}
        disabled={busy}
        aria-label={item.completed ? 'Mark incomplete' : 'Mark complete'}
      />
      {editing ? (
        <input
          ref={inputRef}
          className="text-edit"
          value={editValue}
          onChange={(e) => {
            setEditValue(e.target.value);
            setRowError(null);
          }}
          onBlur={cancelEdit}
          onKeyDown={onKeyDown}
          maxLength={500}
        />
      ) : (
        <span className="text" onClick={startEdit}>
          {item.text}
        </span>
      )}
      {rowError && <span className="row-error">{rowError}</span>}
      <button
        type="button"
        className="delete"
        onClick={remove}
        disabled={busy}
        title="Delete"
        aria-label="Delete item"
      >
        ×
      </button>
    </div>
  );
}
