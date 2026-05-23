import { ErrorOverlay } from './components/ErrorOverlay';
import { LoginPage } from './pages/LoginPage';
import { AppPage } from './pages/AppPage';
import { isAuthed } from './auth';

function App() {
  const onLoginRoute = window.location.pathname === '/login';
  const showLogin = onLoginRoute || !isAuthed();

  return (
    <>
      <ErrorOverlay />
      {showLogin ? <LoginPage /> : <AppPage />}
    </>
  );
}

export default App;
