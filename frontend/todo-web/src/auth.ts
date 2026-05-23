import type { LoginReason } from './types';

const TOKEN_KEY = 'token';
const ACTIVE_LIST_KEY = 'activeListId';

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

export function isAuthed(): boolean {
  return getToken() !== null;
}

export function getActiveListId(): string | null {
  return localStorage.getItem(ACTIVE_LIST_KEY);
}

export function setActiveListId(id: string): void {
  localStorage.setItem(ACTIVE_LIST_KEY, id);
}

export function clearActiveListId(): void {
  localStorage.removeItem(ACTIVE_LIST_KEY);
}

export function goToLogin(reason?: LoginReason): void {
  clearToken();
  clearActiveListId();
  window.location.href = reason ? `/login?reason=${reason}` : '/login';
}
