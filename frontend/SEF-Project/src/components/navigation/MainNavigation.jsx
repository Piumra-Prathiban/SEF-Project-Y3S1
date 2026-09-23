import { navigateTo } from '../../hooks/useLocation';
import { useAuth } from '../../contexts/AuthContext';
import { hasAnyRole } from '../../utils/roles';

export function MainNavigation({ currentPath, routes }) {
  const { user } = useAuth();
  const visibleRoutes = routes.filter((route) => route.showInNavigation);

  function handleNavigate(event, path) {
    event.preventDefault();
    navigateTo(path);
  }

  return (
    <nav className="main-navigation" aria-label="Member 1 navigation">
      {visibleRoutes.map((route) => {
        const isAllowed = hasAnyRole(user, route.roles);

        return (
          <a
            aria-current={currentPath === route.path ? 'page' : undefined}
            className={!isAllowed ? 'main-navigation__link is-disabled' : undefined}
            href={route.path}
            key={route.path}
            onClick={(event) => {
              if (!isAllowed) {
                event.preventDefault();
                return;
              }

              handleNavigate(event, route.path);
            }}
          >
            {route.label}
          </a>
        );
      })}
    </nav>
  );
}
