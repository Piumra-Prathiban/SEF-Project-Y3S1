import { useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

// Treats a 401 from the API as an expired or invalid session. Signing out puts
// the app back into its existing "not authenticated" state, so the
// ProtectedRoute redirects to the login page exactly as it does on a fresh
// visit - no parallel auth mechanism is introduced.
export function useSessionGuard() {
  const { logout } = useAuth();
  const navigate = useNavigate();

  return useCallback(
    (error) => {
      if (error?.status !== 401) {
        return false;
      }

      logout();
      navigate('/login', { replace: true });

      return true;
    },
    [logout, navigate],
  );
}
