import { useCallback } from 'react';
import KpiCard from '../../../components/dashboard/KpiCard';
import DashboardSection from '../../../components/dashboard/DashboardSection';
import { useAsync } from '../../../hooks/useAsync';
import { getSalesSummary } from '../../../services/analyticsService';
import { formatMoney } from '../marketingUtils';
import { formatNumber, percentChange, previousRange } from './analyticsUtils';

// Headline KPIs for the period, each compared with the preceding period.
export default function KpiSection({ token, range, refreshKey }) {
  const loader = useCallback(async () => {
    const [current, previous] = await Promise.all([
      getSalesSummary(token, { from: range.from, to: range.to }),
      getSalesSummary(token, previousRange(range)),
    ]);
    return { current, previous };
  }, [token, range]);
  const { data, error, loading, reload } = useAsync(loader, refreshKey);

  const current = data?.current;
  const previous = data?.previous;
  const change = (field) =>
    previous && previous.orderCount > 0 ? percentChange(current[field], previous[field]) : null;

  return (
    <DashboardSection
      id="kpis"
      title="Key figures"
      description="Confirmed, preparing, ready and completed orders. Change is against the previous period of the same length."
      loading={loading}
      error={error}
      onRetry={reload}
      hasData={Boolean(data)}
      isEmpty={false}
    >
      {current && (
        <>
          <div className="kpi-grid">
            <KpiCard label="Total revenue" value={formatMoney(current.netRevenue)} change={change('netRevenue')} />
            <KpiCard label="Total orders" value={formatNumber(current.orderCount)} change={change('orderCount')} />
            <KpiCard
              label="Average order value"
              value={formatMoney(current.averageOrderValue)}
              change={change('averageOrderValue')}
            />
            <KpiCard label="Units sold" value={formatNumber(current.unitsSold)} change={change('unitsSold')} />
          </div>
          {current.orderCount === 0 && (
            <p className="data-note">No sales were recorded in this period.</p>
          )}
        </>
      )}
    </DashboardSection>
  );
}
