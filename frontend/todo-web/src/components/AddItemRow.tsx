import { useState, type SubmitEvent } from 'react';
import { createItem } from '../api';
import type { ApiFailure } from '../types';

type Props = {
  listId: string;
  onAdded: () => void | Promise<void>;
};

export function AddItemRow({ listId, onAdded }: Props) {
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const trimmed = text.trim();
  const canSubmit = trimmed.length > 0 && !busy;

  const onSubmit = async (e: SubmitEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!canSubmit) return;
    setBusy(true);
    setError(null);
    try {
      await createItem(listId, trimmed);
      setText('');
      await onAdded();
    } catch (err) {
      const f = err as ApiFailure;
      if (f.kind === 'request') setError(f.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="add-item-row" onSubmit={onSubmit}>
      <div className="row">
        <input
          type="text"
          placeholder="Add an item…"
          value={text}
          onChange={(e) => {
            setText(e.target.value);
            setError(null);
          }}
          maxLength={500}
          className={error ? 'has-error' : ''}
        />
        <button type="submit" className="btn btn-primary" disabled={!canSubmit}>
          Add
        </button>
      </div>
      {error && <div className="inline-error">{error}</div>}
    </form>
  );
}
