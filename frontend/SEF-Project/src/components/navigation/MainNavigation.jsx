import { NavLink } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { hasAnyRole } from '../../utils/roles';

export function MainNavigation({ routes }) {
  const { user } = useAuth();
  const visibleRoutes = routes.filter((route) => route.showInNavigation);

  return (
    <nav className="main-navigation" aria-label="Main navigation">
      {visibleRoutes.map((route) => {
        if (!hasAnyRole(user, route.roles)) {
          return (
            <a
              aria-disabled="true"
              className="main-navigation__link is-disabled"
              key={route.path}
              tabIndex={-1}
            >
              {route.label}
            </a>
          );
        }

        return (
          <NavLink
            className="main-navigation__link"
            key={route.path}
            to={route.path}
          >
            {route.label}
          </NavLink>
        );
      })}
    </nav>
  );
}
