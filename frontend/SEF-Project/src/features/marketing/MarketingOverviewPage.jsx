import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import PageHeader from '../../components/PageHeader';
import { ErrorState, LoadingState } from '../../components/StatusViews';
import { useAuth } from '../../contexts/AuthContext';
import { useAsync } from '../../hooks/useAsync';
import { getCampaigns } from '../../services/campaignService';
import { getPromotionPerformance, getPromotions } from '../../services/promotionService';
import { CAMPAIGN_STATUSES } from './marketingConstants';

export default function MarketingOverviewPage() {
  const { token } = useAuth();

  const loader = useCallback(async () => {
    const [promotions, performance, campaigns, activeCampaigns] = await Promise.all([
      getPromotions(token, { pageSize: 1 }),
      getPromotionPerformance(token, { pageSize: 1 }),
      getCampaigns(token, { pageSize: 1 }),
      getCampaigns(token, { pageSize: 1, status: CAMPAIGN_STATUSES.ACTIVE }),
    ]);

    return {
      promotions: promotions.totalCount,
      livePromotions: performance.livePromotionCount,
      campaigns: campaigns.totalCount,
      activeCampaigns: activeCampaigns.totalCount,
    };
  }, [token]);

  const { data, error, loading, reload } = useAsync(loader);

  return (
    <>
      <PageHeader
        title="Marketing"
        subtitle="Manage promotions and campaigns."
        actions={
          <>
            <Link className="button" to="/marketing/promotions/new">New promotion</Link>
            <Link className="button button-secondary" to="/marketing/campaigns/new">
              New campaign
            </Link>
          </>
        }
      />

      {loading && <LoadingState />}
      {error && <ErrorState error={error} onRetry={reload} />}
      {data && (
        <ul className="stat-grid">
          <StatCard label="Live promotions" value={data.livePromotions} to="/marketing/promotions?isActive=true" />
          <StatCard label="All promotions" value={data.promotions} to="/marketing/promotions" />
          <StatCard
            label="Active campaigns"
            value={data.activeCampaigns}
            to={`/marketing/campaigns?status=${CAMPAIGN_STATUSES.ACTIVE}`}
          />
          <StatCard label="All campaigns" value={data.campaigns} to="/marketing/campaigns" />
        </ul>
      )}
    </>
  );
}

function StatCard({ label, value, to }) {
  return (
    <li className="stat-card">
      <Link to={to}>
        <span className="stat-value">{value}</span>
        <span className="stat-label">{label}</span>
      </Link>
    </li>
  );
}
