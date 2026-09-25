import { NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';

export default function AppShell() {
  const { isAuthenticated, user, logout } = useAuth();

  return (
    <div className="app-shell">
      <header className="site-header">
        <NavLink className="brand" to="/products" aria-label="Mode home">
          <span className="brand-mark">M</span>
          <span>Mode</span>
        </NavLink>

        <nav className="main-nav" aria-label="Main navigation">
          <NavLink to="/products">Shop</NavLink>
          {isAuthenticated && <NavLink to="/wishlist">Wishlist</NavLink>}
          {isAuthenticated && <NavLink to="/cart">Cart</NavLink>}
          {isAuthenticated && <NavLink to="/profile">Profile</NavLink>}
        </nav>

        <div className="account-actions">
          {isAuthenticated ? (
            <>
              <span className="account-name">{user?.firstName || user?.email}</span>
              <button type="button" className="text-button" onClick={logout}>Log out</button>
            </>
          ) : (
            <NavLink className="button button--small" to="/login">Log in</NavLink>
          )}
        </div>
      </header>

      <main className="page-container">
        <Outlet />
      </main>

      <footer className="site-footer">
        <span>Mode customer experience</span>
        <span>Secure shopping powered by the SEF API</span>
      </footer>
    </div>
  );
}
