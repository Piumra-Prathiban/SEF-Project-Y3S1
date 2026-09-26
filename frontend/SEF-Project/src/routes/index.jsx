import { Outlet, Route, Routes } from 'react-router-dom';
import { AppLayout } from '../components/layout/AppLayout';
import { PlaceholderPage } from '../components/ui/PlaceholderPage';
import LoginPage from '../features/auth/LoginPage';
import RegisterPage from '../features/auth/RegisterPage';
import { CartPage } from '../features/cart/CartPage';
import { CheckoutPage } from '../features/cart/CheckoutPage';
import AgentWorkflowDetailPage from '../features/marketing/agent/AgentWorkflowDetailPage';
import AgentWorkflowListPage from '../features/marketing/agent/AgentWorkflowListPage';
import AgentWorkflowStartPage from '../features/marketing/agent/AgentWorkflowStartPage';
import AnalyticsPage from '../features/marketing/analytics/AnalyticsPage';
import DashboardPage from '../features/marketing/analytics/DashboardPage';
import ReportsPage from '../features/marketing/analytics/ReportsPage';
import CampaignDetailPage from '../features/marketing/campaigns/CampaignDetailPage';
import CampaignFormPage from '../features/marketing/campaigns/CampaignFormPage';
import CampaignListPage from '../features/marketing/campaigns/CampaignListPage';
import MarketingLayout from '../features/marketing/MarketingLayout';
import MarketingOverviewPage from '../features/marketing/MarketingOverviewPage';
import PromotionDetailPage from '../features/marketing/promotions/PromotionDetailPage';
import PromotionFormPage from '../features/marketing/promotions/PromotionFormPage';
import PromotionListPage from '../features/marketing/promotions/PromotionListPage';
import OrderDetailPage from '../features/orders/OrderDetailPage';
import { ProductDetailPage } from '../features/storefront/ProductDetailPage';
import { StorefrontPage } from '../features/storefront/StorefrontPage';
import { StorefrontChrome } from '../features/storefront/StorefrontChrome';
import { STAFF_ROLES } from '../utils/roles';
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
      <Route path="/shop/:id" element={<StorefrontChrome><ProductDetailPage /></StorefrontChrome>} />
      <Route path="/cart" element={<StorefrontChrome><CartPage /></StorefrontChrome>} />
      <Route path="/checkout" element={<ProtectedRoute><StorefrontChrome><CheckoutPage /></StorefrontChrome></ProtectedRoute>} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      <Route element={<ProtectedLayout />}>
        {/* Nav-only entries carry no element: their routes are declared below. */}
        {memberOneRoutes.filter((route) => route.element).map((route) => (
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
          path="/marketing"
          element={(
            <ProtectedRoute roles={STAFF_ROLES}>
              <MarketingLayout />
            </ProtectedRoute>
          )}
        >
          <Route index element={<MarketingOverviewPage />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="analytics" element={<AnalyticsPage />} />
          <Route path="reports" element={<ReportsPage />} />
          <Route path="promotions" element={<PromotionListPage />} />
          <Route path="promotions/new" element={<PromotionFormPage />} />
          <Route path="promotions/:id" element={<PromotionDetailPage />} />
          <Route path="promotions/:id/edit" element={<PromotionFormPage />} />
          <Route path="campaigns" element={<CampaignListPage />} />
          <Route path="campaigns/new" element={<CampaignFormPage />} />
          <Route path="campaigns/:id" element={<CampaignDetailPage />} />
          <Route path="campaigns/:id/edit" element={<CampaignFormPage />} />
          <Route path="agent" element={<AgentWorkflowListPage />} />
          <Route path="agent/new" element={<AgentWorkflowStartPage />} />
          <Route path="agent/:id" element={<AgentWorkflowDetailPage />} />
        </Route>

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
