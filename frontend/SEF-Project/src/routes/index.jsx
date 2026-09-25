import { Outlet, Route, Routes } from 'react-router-dom';
import { AppLayout } from '../components/layout/AppLayout';
import { PlaceholderPage } from '../components/ui/PlaceholderPage';
import LoginPage from '../features/auth/LoginPage';
import { CartPage } from '../features/cart/CartPage';
import { CheckoutPage } from '../features/cart/CheckoutPage';
import OrderDetailPage from '../features/orders/OrderDetailPage';
import { ProductDetailPage } from '../features/storefront/ProductDetailPage';
import { StorefrontPage } from '../features/storefront/StorefrontPage';
import { memberOneRoutes } from './routeConfig.jsx';
import { ProtectedRoute } from './ProtectedRoute';

function ProtectedLayout() {
  return (
    <ProtectedRoute>
      <AppLayout routes={memberOneRoutes}>
        <Outlet />
      </AppLayout>
    </ProtectedRoute>
  );
}

function AppRoutes() {
  return (
    <Routes>
      {/* Public storefront: browsable without signing in. */}
      <Route path="/" element={<StorefrontPage />} />
      <Route path="/shop/:id" element={<ProductDetailPage />} />
      <Route path="/cart" element={<CartPage />} />
      <Route path="/login" element={<LoginPage />} />

      <Route element={<ProtectedLayout />}>
        <Route path="/checkout" element={<CheckoutPage />} />
        {memberOneRoutes.map((route) => (
          <Route
            key={route.path}
            path={route.path}
            element={
              route.roles ? (
                <ProtectedRoute roles={route.roles}>{route.element}</ProtectedRoute>
              ) : (
                route.element
              )
            }
          />
        ))}

        <Route path="/orders/:id" element={<OrderDetailPage />} />

        <Route
          path="*"
          element={
            <PlaceholderPage
              area="Page not found"
              description="The requested frontend route does not exist."
            />
          }
        />
      </Route>
    </Routes>
  );
}

export default AppRoutes;
