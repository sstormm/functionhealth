import { useState, type ChangeEvent, type SubmitEvent } from 'react';
import { login } from '../api';
import type { ApiFailure, LoginReason } from '../types';

const REASON_MESSAGES: Record<LoginReason, string> = {
  system_error: 'The system is having problems at the moment. Please try again in a few minutes.',
};
const CREDENTIAL_ERROR_TEXT = "Invalid username or password.";
const CONNECTION_ERROR_TEXT = 'Connection error — cannot reach server. Try again later.';
const GENERIC_ERROR_TEXT = 'Unknown error occurred. Please try again.';

function readReason(): LoginReason | null {
  const raw = new URLSearchParams(window.location.search).get('reason');
  return raw === 'system_error' ? raw : null;
}

export function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [credentialError, setCredentialError] = useState(false);
  const [errorText, setErrorText] = useState<string | null>(null);
  const [reason, setReason] = useState<LoginReason | null>(readReason);

  const message = errorText ?? (reason ? REASON_MESSAGES[reason] : null);
  const canSubmit = username.trim().length > 0 && password.length > 0;

  const clearMessages = () => {
    if (credentialError) setCredentialError(false);
    if (errorText) setErrorText(null);
    if (reason) setReason(null);
  };

  const onUsernameChange = (e: ChangeEvent<HTMLInputElement>) => {
    setUsername(e.target.value);
    clearMessages();
  };

  const onPasswordChange = (e: ChangeEvent<HTMLInputElement>) => {
    setPassword(e.target.value);
    clearMessages();
  };

  const onSubmit = async (e: SubmitEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (busy) return;
    setReason(null); // submit always dismisses the reason banner
    setErrorText(null);
    setCredentialError(false);
    setBusy(true);
    try {
      await login(username, password);
      window.location.href = '/';
    } catch (err) {
      const failure = err as ApiFailure;
      if (failure.kind === 'request') {
        if (failure.code === 'INVALID_CREDENTIALS') {
          setCredentialError(true);
          setErrorText(CREDENTIAL_ERROR_TEXT);
        } else {
          setErrorText(GENERIC_ERROR_TEXT);
        }
      } else if (failure.kind === 'system') {
        // Network or 5xx during login — show inline rather than the recovery
        // modal. api.ts suppresses the global SystemErrorStore publish for
        // this call so no overlay mounts.
        setErrorText(failure.mode === 'network' ? CONNECTION_ERROR_TEXT : GENERIC_ERROR_TEXT);
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-screen">
      <form className="login-card" onSubmit={onSubmit} noValidate>
        <h1>Sign in</h1>
        <p className="sub">Welcome back to todo.</p>

        <label className="field">
          <span>Username</span>
          <input
            type="text"
            value={username}
            onChange={onUsernameChange}
            autoComplete="username"
            autoFocus
          />
        </label>

        <label className="field">
          <span>Password</span>
          <input
            type="password"
            value={password}
            onChange={onPasswordChange}
            autoComplete="current-password"
            className={credentialError ? 'has-error' : ''}
          />
        </label>

        {message && <div className="inline-error">{message}</div>}

        <button type="submit" className="btn btn-primary" disabled={busy || !canSubmit}>
          Sign in
        </button>
      </form>
    </div>
  );
}
