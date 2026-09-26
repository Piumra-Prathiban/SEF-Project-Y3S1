import { NavLink } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { hasAnyRole } from '../../utils/roles';

const GROUPS = [
  { label: 'Workspace', paths: ['/dashboard', '/orders', '/wishlist', '/stylist', '/profile'] },
  { label: 'Commerce', paths: ['/products', '/variants', '/inventory', '/inventory/history', '/inventory/low-stock', '/purchase-orders'] },
  { label: 'Catalogue', paths: ['/categories', '/collections', '/sizes', '/colours', '/suppliers'] },
  { label: 'Intelligence', paths: ['/marketing', '/reviews', '/inventory-agent'] },
];

function NavigationItem({ route }) {
  return <NavLink className="main-navigation__link" end to={route.path}>{route.label}</NavLink>;
}

export function MainNavigation({ routes }) {
  const { user } = useAuth();
  const visibleRoutes = routes.filter((route) => route.showInNavigation && hasAnyRole(user, route.roles));

  return (
    <nav className="main-navigation" aria-label="Main navigation">
      {GROUPS.map((group) => {
        const items = group.paths.map((path) => visibleRoutes.find((route) => route.path === path)).filter(Boolean);
        if (items.length === 0) return null;
        return (
          <div className="main-navigation__group" key={group.label}>
            <p className="main-navigation__heading">{group.label}</p>
            {items.map((route) => <NavigationItem key={route.path} route={route} />)}
          </div>
        );
      })}
    </nav>
  );
}
