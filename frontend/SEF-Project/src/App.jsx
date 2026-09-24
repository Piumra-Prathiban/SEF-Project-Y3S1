import { Link } from 'react-router-dom';
import { useAuth } from './contexts/AuthContext';
import AppRoutes from './routes';
import './App.css';

function App() {
  const { user, isAuthenticated, logout } = useAuth();

  return (
    <div className="app">
      <header className="app-header">
        <Link to="/" className="app-header__brand">
          SEF Fashion
        </Link>

        {isAuthenticated && (
          <nav className="app-header__nav">
            <Link to="/orders">Orders</Link>
            <span className="app-header__user">{user?.email}</span>
            <button type="button" onClick={logout}>
              Logout
            </button>
          </nav>
        )}
      </header>

      <main className="app-main">
        <AppRoutes />
      </main>
    </div>
  );
}

export default App;
