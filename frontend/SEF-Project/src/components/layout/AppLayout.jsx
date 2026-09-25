import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { MainNavigation } from '../navigation/MainNavigation';

export function AppLayout({ children, routes }) {
  const { user, logout } = useAuth();

  return (
    <div className="app-layout">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>

      <header className="app-header">
        <div>
          <p className="app-header__eyebrow">SE3090 Group Project</p>
          <h1 className="app-header__title">Clothic</h1>
        </div>

        <div className="app-header__user">
          <Link className="app-header__storefront" to="/">
            Storefront
          </Link>
          <span>{user?.email}</span>
          <span className="role-badge">{user?.role}</span>
          <button type="button" onClick={logout}>
            Logout
          </button>
        </div>
      </header>

      <div className="app-layout__body">
        <aside className="app-layout__sidebar">
          <MainNavigation routes={routes} />
        </aside>

        <main className="app-layout__content" id="main-content" tabIndex={-1}>
          {children}
        </main>
      </div>
    </div>
  );
}
