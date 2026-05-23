import { useState } from 'react';
import { probeGet, probeMe } from '../api';
import { getActiveListId, goToLogin } from '../auth';
import { clearSystemError, useSystemError } from '../systemError';

export function ErrorOverlay() {
  const error = useSystemError();
  if (!error) return null;

  if (error.mode === 'network') {
    return <NetworkModal />;
  }
  return <ServerModal />;
}

function NetworkModal() {
  const [busy, setBusy] = useState(false);

  const onRetry = async () => {
    if (busy) return;
    setBusy(true);
    const result = await probeMe();
    setBusy(false);
    if (result === 'ok') {
      clearSystemError();
      return;
    }
    if (result === 'unauthorized') {
      goToLogin();
      return;
    }
    // still down — modal stays; user can click Retry again
  };

  return (
    <div className="overlay-backdrop">
      <div className="overlay-modal">
        <h2>Unable to reach server</h2>
        <p>Sorry for the inconvenience. Click Retry to try again.</p>
        <div className="actions">
          <button type="button" onClick={onRetry} disabled={busy}>
            Retry
          </button>
        </div>
      </div>
    </div>
  );
}

function ServerModal() {
  const [busy, setBusy] = useState(false);

  const onOk = async () => {
    if (busy) return;
    setBusy(true);

    const meResult = await probeGet('/api/auth/me');
    if (meResult === 'unauthorized') {
      goToLogin();
      return;
    }
    if (meResult === 'down') {
      goToLogin('system_error');
      return;
    }

    const listsResult = await probeGet('/api/lists');
    if (listsResult === 'unauthorized') {
      goToLogin();
      return;
    }
    if (listsResult === 'down') {
      goToLogin('system_error');
      return;
    }

    const activeListId = getActiveListId();
    if (activeListId) {
      const itemsResult = await probeGet(`/api/lists/${activeListId}/items`);
      if (itemsResult === 'unauthorized') {
        goToLogin();
        return;
      }
      if (itemsResult === 'down') {
        goToLogin('system_error');
        return;
      }
    }

    clearSystemError();
    window.location.reload();
  };

  return (
    <div className="overlay-backdrop">
      <div className="overlay-modal">
        <h2>Something went wrong on our end.</h2>
        <p>The system is experiencing problems. Click OK to refresh your view.</p>
        <div className="actions">
          <button type="button" onClick={onOk} disabled={busy}>
            OK
          </button>
        </div>
      </div>
    </div>
  );
}
