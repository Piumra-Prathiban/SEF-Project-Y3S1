import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { MANAGER_ROLES } from '../features/marketing/marketingConstants';

export default function AppLayout() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();
  const canManageMarketing = isAuthenticated && MANAGER_ROLES.includes(user?.role);

  function handleLogout() {
    logout();
    navigate('/login');
  }

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">Skip to content</a>
      <header className="app-header">
        <NavLink to="/" className="brand">SEF Project</NavLink>
        <nav aria-label="Main">
          <ul className="nav-list">
            <li><NavLink to="/" end>Home</NavLink></li>
            {canManageMarketing && (
              <li><NavLink to="/marketing">Marketing</NavLink></li>
            )}
          </ul>
        </nav>
        <div className="app-user">
          {isAuthenticated ? (
            <>
              <span className="user-label">
                {user?.email} <span className="muted">({user?.role})</span>
              </span>
              <button type="button" className="button button-secondary" onClick={handleLogout}>
                Log out
              </button>
            </>
          ) : (
            <NavLink className="button" to="/login">Log in</NavLink>
          )}
        </div>
      </header>
      <main id="main" className="app-main">
        <Outlet />
      </main>
    </div>
  );
}
