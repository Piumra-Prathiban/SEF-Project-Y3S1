import { useCallback } from 'react';
import { ErrorState, LoadingState } from '../../components/StatusViews';
import { useAuth } from '../../contexts/AuthContext';
import { useAsync } from '../../hooks/useAsync';
import { getCampaigns } from '../../services/campaignService';
import { getPromotionPerformance, getPromotions } from '../../services/promotionService';
import { CAMPAIGN_STATUSES } from './marketingConstants';

export function PromotionStats() {
  const { token } = useAuth();

  const loader = useCallback(async () => {
    const [promotions, performance] = await Promise.all([
      getPromotions(token, { pageSize: 1 }),
      getPromotionPerformance(token, { pageSize: 1 }),
    ]);
    return { total: promotions.totalItems, active: performance.livePromotionCount };
  }, [token]);

  const { data, error, loading, reload } = useAsync(loader);

  return (
    <StatusBox
      title="Promotions"
      inactiveLabel="not live"
      data={data}
      error={error}
      loading={loading}
      reload={reload}
    />
  );
}

export function CampaignStats() {
  const { token } = useAuth();

  const loader = useCallback(async () => {
    const [campaigns, activeCampaigns] = await Promise.all([
      getCampaigns(token, { pageSize: 1 }),
      getCampaigns(token, { pageSize: 1, status: CAMPAIGN_STATUSES.ACTIVE }),
    ]);
    return { total: campaigns.totalItems, active: activeCampaigns.totalItems };
  }, [token]);

  const { data, error, loading, reload } = useAsync(loader);

  return (
    <StatusBox
      title="Campaigns"
      inactiveLabel="not active"
      data={data}
      error={error}
      loading={loading}
      reload={reload}
    />
  );
}

function StatusBox({ title, inactiveLabel, data, error, loading, reload }) {
  if (loading && !data) return <LoadingState />;
  if (error) return <ErrorState error={error} onRetry={reload} />;
  if (!data) return null;

  const segments = Math.min(data.total, 40);
  const filled = data.total ? Math.round((data.active / data.total) * segments) : 0;

  return (
    <section className="status-box" aria-label={`${title} summary`}>
      <span className="status-box-title">{title}</span>
      <span className="status-box-value">{data.total}</span>
      <div className="status-bar" aria-hidden="true">
        {Array.from({ length: segments }, (_, index) => (
          <span key={index} className={index < filled ? 'status-bar-on' : 'status-bar-off'} />
        ))}
      </div>
      <span className="status-box-caption">
        <strong>{data.active} active</strong> · {data.total - data.active} {inactiveLabel}
      </span>
    </section>
  );
}
