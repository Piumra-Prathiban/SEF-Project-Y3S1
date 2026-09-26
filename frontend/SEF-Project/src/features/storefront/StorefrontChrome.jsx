import { Link, NavLink } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { isStaff } from '../../utils/roles';
import { useCart } from '../cart/CartContext';

export function StorefrontChrome({ children }) {
  const { isAuthenticated, user, logout } = useAuth();
  const { itemCount } = useCart();

  return (
    <div className="storefront">
      <div className="storefront__announcement">Considered essentials for every day <span aria-hidden="true">✦</span> Shop the collection</div>
      <header className="storefront__header">
        <Link className="storefront__brand" to="/" aria-label="Clothic home">CLOTHIC<span>®</span></Link>
        <nav className="storefront__nav" aria-label="Storefront navigation">
          <NavLink to="/" end>Shop</NavLink>
          <a href="/#catalogue">The collection</a>
          {isAuthenticated && !isStaff(user) && <NavLink to="/stylist">Stylist</NavLink>}
          {isAuthenticated && <NavLink to="/orders">My orders</NavLink>}
        </nav>
        <div className="storefront__actions">
          {isAuthenticated ? <>
            <Link className="storefront__account" to={isStaff(user) ? '/dashboard' : '/profile'}>{isStaff(user) ? 'Dashboard' : 'Account'}</Link>
            <button className="storefront__logout" onClick={logout} type="button">Sign out</button>
          </> : <Link className="storefront__sign-in" to="/login">Sign in</Link>}
          <Link className="storefront__cart" to="/cart">Cart{itemCount > 0 ? ` (${itemCount})` : ''}<span aria-hidden="true">↗</span></Link>
        </div>
      </header>
      {children}
      <footer className="storefront__footer"><span className="storefront__footer-brand">CLOTHIC®</span><span>Made for the moments in between.</span><span>© {new Date().getFullYear()} Clothic</span></footer>
    </div>
  );
}
