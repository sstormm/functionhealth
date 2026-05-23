import { useEffect, useRef, useState } from 'react';
import type { User } from '../types';

type Props = {
  user: User;
  onSignOut: () => void | Promise<void>;
};

export function UserMenu({ user, onSignOut }: Props) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onMouseDown = (e: globalThis.MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    window.addEventListener('mousedown', onMouseDown);
    window.addEventListener('keydown', onKey);
    return () => {
      window.removeEventListener('mousedown', onMouseDown);
      window.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const initial = user.email[0]?.toUpperCase() ?? '?';

  return (
    <div className="user-menu" ref={ref}>
      <button
        type="button"
        className="user-pill"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        <span className="avatar">{initial}</span>
        <span>{user.email}</span>
        <span aria-hidden>▾</span>
      </button>
      {open && (
        <div className="user-popover" role="menu">
          <button
            type="button"
            className="btn-link danger"
            onClick={onSignOut}
          >
            Sign out
          </button>
        </div>
      )}
    </div>
  );
}
