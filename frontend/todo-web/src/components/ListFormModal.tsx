import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { createList, renameList } from '../api';
import type { ApiFailure, ListSummary } from '../types';
import { Modal } from './Modal';

export type ListFormMode =
  | { kind: 'create' }
  | { kind: 'rename'; listId: string; currentName: string };

type Props = {
  mode: ListFormMode;
  existingNamesLower: string[]; // all current list names, lowercased + trimmed
  onClose: () => void;
  onCreated?: (list: ListSummary) => void;
  onRenamed?: (list: ListSummary) => void;
};

export function ListFormModal({ mode, existingNamesLower, onClose, onCreated, onRenamed }: Props) {
  const [name, setName] = useState(mode.kind === 'rename' ? mode.currentName : '');
  const [serverError, setServerError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (mode.kind === 'rename') inputRef.current?.select();
    else inputRef.current?.focus();
  }, [mode.kind]);

  const trimmed = name.trim();
  const lowered = trimmed.toLowerCase();
  const originalLower = mode.kind === 'rename' ? mode.currentName.trim().toLowerCase() : null;

  let validationError: string | null = null;
  let saveEnabled = false;

  if (trimmed === '') {
    // Blank → Save stays disabled, no inline error. The disabled button is
    // sufficient signal; an error message before the user has typed anything
    // (or right after clearing the field) reads as scolding.
  } else if (originalLower !== null && lowered === originalLower) {
    // rename to the same name: no change, save disabled, no error
  } else if (existingNamesLower.includes(lowered)) {
    validationError = `A list named "${trimmed}" already exists.`;
  } else {
    saveEnabled = true;
  }

  const error = serverError ?? validationError;
  const title = mode.kind === 'create' ? 'New list' : 'Rename list';
  const submitText = mode.kind === 'create' ? 'Create list' : 'Save';

  const onSubmit = async (e: SubmitEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!saveEnabled || busy) return;
    setBusy(true);
    setServerError(null);
    try {
      if (mode.kind === 'create') {
        const created = await createList(trimmed);
        onCreated?.(created);
      } else {
        const renamed = await renameList(mode.listId, trimmed);
        onRenamed?.(renamed);
      }
      onClose();
    } catch (err) {
      const f = err as ApiFailure;
      if (f.kind === 'request' && f.code === 'DUPLICATE_NAME') {
        setServerError(`A list named "${trimmed}" already exists.`);
      } else if (f.kind === 'request') {
        setServerError(f.message);
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal onClose={onClose}>
      <form className="modal-form" onSubmit={onSubmit}>
        <h2>{title}</h2>
        <label className="field">
          <span>List name</span>
          <input
            ref={inputRef}
            type="text"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              setServerError(null);
            }}
            placeholder={mode.kind === 'create' ? 'e.g. Reading' : ''}
            className={error ? 'has-error' : ''}
            maxLength={100}
          />
        </label>
        {error && <div className="inline-error">{error}</div>}
        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={!saveEnabled || busy}>
            {submitText}
          </button>
        </div>
      </form>
    </Modal>
  );
}
