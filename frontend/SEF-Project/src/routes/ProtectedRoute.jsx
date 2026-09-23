import { Alert } from '../components/ui/Alert';
import { useAuth } from '../contexts/AuthContext';
import { navigateTo } from '../hooks/useLocation';
import { hasAnyRole } from '../utils/roles';

export function ProtectedRoute({ children, roles }) {
  const { isAuthenticated, user } = useAuth();

  if (!isAuthenticated) {
    navigateTo('/login');
    return null;
  }

  if (!hasAnyRole(user, roles)) {
    return (
      <Alert tone="danger">
        You do not have permission to access this Member 1 page.
      </Alert>
    );
  }

  return children;
}
