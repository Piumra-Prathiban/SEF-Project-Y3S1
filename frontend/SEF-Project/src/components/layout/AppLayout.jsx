import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { isStaff } from '../../utils/roles';
import { MainNavigation } from '../navigation/MainNavigation';

export function AppLayout({ children, routes }) {
  const { user, logout } = useAuth();
  const location = useLocation();
  const currentRoute = routes.find((route) => route.path === location.pathname);
  const firstName = user?.email?.split('@')[0] || 'Account';

  return (
    <div className="app-layout">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>

      <aside className="app-layout__sidebar">
        <Link className="app-brand" to={isStaff(user) ? '/dashboard' : '/orders'} aria-label="Clothic home">
          <span className="app-brand__mark" aria-hidden="true">C</span>
          <h1 aria-label="Clothic" className="app-brand__text">Clothic <small>WORKSPACE</small></h1>
        </Link>
        <div className="app-layout__sidebar-label">Your workspace</div>
        <MainNavigation routes={routes} />
        <Link className="sidebar-store-link" to="/">
          <span>View the storefront</span><span aria-hidden="true">↗</span>
        </Link>
      </aside>

      <div className="app-layout__main">
        <header className="app-header">
          <div className="app-header__title-group">
            <span className="app-header__eyebrow">CLOTHIC / {isStaff(user) ? 'OPERATIONS' : 'MY ACCOUNT'}</span>
            <div className="app-header__title">{currentRoute?.label || (location.pathname.startsWith('/marketing') ? 'Marketing' : 'Workspace')}</div>
          </div>
          <div className="app-header__user">
            <Link className="app-header__storefront" to="/">Visit store <span aria-hidden="true">↗</span></Link>
            <div className="app-user-chip">
              <span className="app-user-chip__avatar" aria-hidden="true">{firstName[0].toUpperCase()}</span>
              <span className="app-user-chip__details"><strong>{firstName}</strong><small>{user?.role}</small></span>
            </div>
            <button className="app-header__logout" type="button" onClick={logout}>Sign out</button>
          </div>
        </header>
        <main className="app-layout__content" id="main-content" tabIndex={-1}>
          {children}
        </main>
      </div>
    </div>
  );
}
