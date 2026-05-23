import { useState } from 'react';
import { deleteList } from '../api';
import type { ApiFailure, ListSummary } from '../types';
import { Modal } from './Modal';

type Props = {
  list: ListSummary;
  onClose: () => void;
  onDeleted: () => void;
};

export function DeleteListModal({ list, onClose, onDeleted }: Props) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onDelete = async () => {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await deleteList(list.id);
      onDeleted();
      onClose();
    } catch (err) {
      const f = err as ApiFailure;
      if (f.kind === 'request') setError(f.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal onClose={onClose}>
      <h2>Delete "{list.name}"?</h2>
      <p className="modal-body">
        {list.openCount} open items and any completed items will be permanently
        removed. This can't be undone.
      </p>
      {error && <div className="inline-error">{error}</div>}
      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose} disabled={busy}>
          Cancel
        </button>
        <button type="button" className="btn btn-danger" onClick={onDelete} disabled={busy}>
          Delete list
        </button>
      </div>
    </Modal>
  );
}
