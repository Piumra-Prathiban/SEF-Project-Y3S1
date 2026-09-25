import { Navigate } from 'react-router-dom';
import { Alert } from '../components/ui/Alert';
import { useAuth } from '../contexts/AuthContext';
import { hasAnyRole } from '../utils/roles';

export function ProtectedRoute({ children, roles }) {
  const { isAuthenticated, user } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!hasAnyRole(user, roles)) {
    return (
      <Alert tone="danger">
        You do not have permission to access this page.
      </Alert>
    );
  }

  return children;
}
