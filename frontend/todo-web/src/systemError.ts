import { useEffect, useSyncExternalStore } from 'react';
import type { SystemErrorMode } from './types';

type State = { mode: SystemErrorMode } | null;
type Listener = () => void;

let current: State = null;
const listeners = new Set<Listener>();

function emit() {
  listeners.forEach((l) => l());
}

export function publishSystemError(mode: SystemErrorMode): void {
  current = { mode };
  emit();
}

export function clearSystemError(): void {
  if (current === null) return;
  current = null;
  emit();
}

function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function getSnapshot(): State {
  return current;
}

export function useSystemError(): State {
  // Re-subscribe defensively across hot reloads, but the store is module-level.
  useEffect(() => {
    // no-op; subscription is handled by useSyncExternalStore
  }, []);
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
