import {
  getToken,
  setToken,
  clearToken,
  goToLogin,
} from './auth';
import { publishSystemError } from './systemError';
import type {
  ApiFailure,
  Item,
  ItemsResponse,
  ListSummary,
  LoginResponse,
  User,
} from './types';

const BASE_URL = 'http://localhost:5000';

type Method = 'GET' | 'POST' | 'PATCH' | 'DELETE';

type RequestOptions = {
  /**
   * When false, system-level failures (network throw, 5xx) are still thrown
   * to the caller but NOT broadcast to the global `SystemErrorStore`, so the
   * `ErrorOverlay` modal won't mount. Used by `login` so the LoginPage can
   * surface the error inline instead of with the post-auth recovery modal.
   */
  publishSystemErrors?: boolean;
};

async function request<T>(
  method: Method,
  path: string,
  body?: unknown,
  options?: RequestOptions,
): Promise<T> {
  const publish = options?.publishSystemErrors ?? true;
  const token = getToken();
  const headers: Record<string, string> = {};
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (token) headers['Authorization'] = `Bearer ${token}`;

  let resp: Response;
  try {
    resp = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    if (publish) publishSystemError('network');
    const failure: ApiFailure = { kind: 'system', mode: 'network' };
    throw failure;
  }

  if (resp.status >= 500) {
    if (publish) publishSystemError('server');
    const failure: ApiFailure = { kind: 'system', mode: 'server' };
    throw failure;
  }

  if (resp.status === 401 && token) {
    // Authenticated request was rejected — session expired/invalid.
    // Redirect silently; the login page doesn't surface a reason banner for
    // this case (stale tokens after a backend restart are common and the
    // "session expired" message just adds noise).
    goToLogin();
    const failure: ApiFailure = { kind: 'session_expired' };
    throw failure;
  }

  if (resp.status === 204) {
    return undefined as T;
  }

  const data = await resp.json().catch(() => null);

  if (!resp.ok) {
    const code: string = data?.error?.code ?? 'UNKNOWN';
    const message: string = data?.error?.message ?? 'Unknown error';
    const failure: ApiFailure = {
      kind: 'request',
      status: resp.status,
      code,
      message,
    };
    throw failure;
  }

  return data as T;
}

/* ---------- Auth ---------- */

export async function login(email: string, password: string): Promise<LoginResponse> {
  // Suppress the global ErrorOverlay modal for login — the LoginPage handles
  // network and 5xx failures inline. The user is at the entry point and has
  // no in-progress state to recover, so a blocking modal is the wrong grain.
  const data = await request<LoginResponse>(
    'POST',
    '/api/auth/login',
    { email, password },
    { publishSystemErrors: false },
  );
  setToken(data.token);
  return data;
}

export async function logout(): Promise<void> {
  try {
    await request<void>('POST', '/api/auth/logout');
  } catch {
    // Best-effort — we're clearing the local token regardless.
  }
  clearToken();
}

export function me(options?: RequestOptions): Promise<User> {
  return request<User>('GET', '/api/auth/me', undefined, options);
}

/* ---------- Lists ---------- */

export function getLists(options?: RequestOptions): Promise<ListSummary[]> {
  return request<ListSummary[]>('GET', '/api/lists', undefined, options);
}

export function createList(name: string): Promise<ListSummary> {
  return request<ListSummary>('POST', '/api/lists', { name });
}

export function renameList(id: string, name: string): Promise<ListSummary> {
  return request<ListSummary>('PATCH', `/api/lists/${id}`, { name });
}

export function deleteList(id: string): Promise<void> {
  return request<void>('DELETE', `/api/lists/${id}`);
}

/* ---------- Items ---------- */

export function getItems(listId: string): Promise<ItemsResponse> {
  return request<ItemsResponse>('GET', `/api/lists/${listId}/items`);
}

export function createItem(listId: string, text: string): Promise<Item> {
  return request<Item>('POST', `/api/lists/${listId}/items`, { text });
}

export function updateItem(
  id: string,
  patch: { text?: string; completed?: boolean },
): Promise<Item> {
  return request<Item>('PATCH', `/api/items/${id}`, patch);
}

export function deleteItem(id: string): Promise<void> {
  return request<void>('DELETE', `/api/items/${id}`);
}

/* ---------- Probes (used by ErrorOverlay) ---------- */

export type ProbeResult = 'ok' | 'unauthorized' | 'down';

/**
 * Raw GET that never publishes a system error and never redirects.
 * Used by ErrorOverlay's retry / soft-reload paths to inspect server state
 * without recursively triggering modals.
 */
export async function probeGet(path: string): Promise<ProbeResult> {
  const token = getToken();
  const headers: Record<string, string> = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;
  try {
    const resp = await fetch(`${BASE_URL}${path}`, { method: 'GET', headers });
    if (resp.ok) return 'ok';
    if (resp.status === 401) return 'unauthorized';
    return 'down';
  } catch {
    return 'down';
  }
}

export function probeMe(): Promise<ProbeResult> {
  return probeGet('/api/auth/me');
}
