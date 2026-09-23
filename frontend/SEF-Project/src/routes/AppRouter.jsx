import { AppLayout } from '../components/layout/AppLayout';
import { PlaceholderPage } from '../components/ui/PlaceholderPage';
import { LoginPage } from '../features/auth/LoginPage';
import { useAuth } from '../contexts/AuthContext';
import { navigateTo, useLocation } from '../hooks/useLocation';
import { memberOneRoutes } from './routeConfig.jsx';
import { ProtectedRoute } from './ProtectedRoute';

export function AppRouter() {
  const currentPath = useLocation();
  const { isAuthenticated } = useAuth();

  if (currentPath === '/' && isAuthenticated) {
    navigateTo('/products');
    return null;
  }

  if (currentPath === '/' || currentPath === '/login') {
    return <LoginPage />;
  }

  const route = memberOneRoutes.find((item) => item.path === currentPath);

  return (
    <AppLayout currentPath={currentPath} routes={memberOneRoutes}>
      <ProtectedRoute roles={route?.roles}>
        {route?.element ?? (
          <PlaceholderPage
            area="Page not found"
            description="The requested frontend route does not exist."
          />
        )}
      </ProtectedRoute>
    </AppLayout>
  );
}
