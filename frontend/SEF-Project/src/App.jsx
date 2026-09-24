import { Link, Route, Routes } from 'react-router-dom';
import AppLayout from './components/AppLayout';
import LoginPage from './features/auth/LoginPage';
import HomePage from './features/home/HomePage';
import CampaignDetailPage from './features/marketing/campaigns/CampaignDetailPage';
import CampaignFormPage from './features/marketing/campaigns/CampaignFormPage';
import CampaignListPage from './features/marketing/campaigns/CampaignListPage';
import MarketingLayout from './features/marketing/MarketingLayout';
import MarketingOverviewPage from './features/marketing/MarketingOverviewPage';
import { MANAGER_ROLES } from './features/marketing/marketingConstants';
import PromotionDetailPage from './features/marketing/promotions/PromotionDetailPage';
import PromotionFormPage from './features/marketing/promotions/PromotionFormPage';
import PromotionListPage from './features/marketing/promotions/PromotionListPage';
import ProtectedRoute from './routes/ProtectedRoute';

export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<HomePage />} />
        <Route path="login" element={<LoginPage />} />

        <Route element={<ProtectedRoute roles={MANAGER_ROLES} />}>
          <Route path="marketing" element={<MarketingLayout />}>
            <Route index element={<MarketingOverviewPage />} />
            <Route path="promotions" element={<PromotionListPage />} />
            <Route path="promotions/new" element={<PromotionFormPage />} />
            <Route path="promotions/:id" element={<PromotionDetailPage />} />
            <Route path="promotions/:id/edit" element={<PromotionFormPage />} />
            <Route path="campaigns" element={<CampaignListPage />} />
            <Route path="campaigns/new" element={<CampaignFormPage />} />
            <Route path="campaigns/:id" element={<CampaignDetailPage />} />
            <Route path="campaigns/:id/edit" element={<CampaignFormPage />} />
          </Route>
        </Route>

        <Route
          path="*"
          element={
            <section>
              <h1>Page not found</h1>
              <Link to="/">Go to home</Link>
            </section>
          }
        />
      </Route>
    </Routes>
  );
}
