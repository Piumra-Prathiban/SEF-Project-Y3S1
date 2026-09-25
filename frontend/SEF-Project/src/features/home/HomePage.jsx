import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { MANAGER_ROLES } from '../marketing/marketingConstants';

export default function HomePage() {
  const { isAuthenticated, user } = useAuth();

  return (
    <section>
      <h1>SEF Project</h1>
      {!isAuthenticated && (
        <p>
          <Link to="/login">Log in</Link> to continue.
        </p>
      )}
      {isAuthenticated && <p>Welcome, {user?.email}.</p>}
      {isAuthenticated && MANAGER_ROLES.includes(user?.role) && (
        <p>
          <Link className="button" to="/marketing">Go to Marketing</Link>
        </p>
      )}
    </section>
  );
}
