export type User = {
  id: string;
  email: string;
};

export type LoginResponse = {
  token: string;
  user: User;
};

export type ListSummary = {
  id: string;
  name: string;
  updatedAt: string;
  openCount: number;
  doneCount: number;
};

export type Item = {
  id: string;
  listId: string;
  text: string;
  completed: boolean;
  order: number;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string;
};

export type ItemsResponse = {
  open: Item[];
  completed: Item[];
};

export type ApiErrorBody = {
  code: string;
  message: string;
};

export type SystemErrorMode = 'network' | 'server';

export type ApiFailure =
  | { kind: 'request'; status: number; code: string; message: string }
  | { kind: 'session_expired' }
  | { kind: 'system'; mode: SystemErrorMode };

export type LoginReason = 'system_error';
