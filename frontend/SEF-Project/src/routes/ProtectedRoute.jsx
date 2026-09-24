import { Link, Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

// Requires a logged-in user and, optionally, one of `roles`.
// The API still enforces authorization; this only guards navigation.
export default function ProtectedRoute({ roles }) {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (roles && !roles.includes(user?.role)) {
    return (
      <section className="state state-error" role="alert">
        <h1>Access denied</h1>
        <p>Your account does not have permission to view this page.</p>
        <Link className="button" to="/">Go to home</Link>
      </section>
    );
  }

  return <Outlet />;
}
