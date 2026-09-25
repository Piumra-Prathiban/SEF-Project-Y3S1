import { useCallback, useState } from 'react';
import LineChart from '../../../../components/charts/LineChart';
import DashboardSection from '../../../../components/dashboard/DashboardSection';
import { useAsync } from '../../../../hooks/useAsync';
import { getSalesOverTime } from '../../../../services/analyticsService';
import { formatMoney } from '../../marketingUtils';
import {
  defaultGranularity,
  formatCompact,
  formatNumber,
  GRANULARITY,
  toTrendPoints,
  TREND_METRICS,
} from '../analyticsUtils';

export default function RevenueTrendSection({ token, range, refreshKey }) {
  const [metric, setMetric] = useState('revenue');
  const [granularity, setGranularity] = useState('auto');

  const resolvedGranularity =
    granularity === 'auto' ? defaultGranularity(range.days) : Number(granularity);

  const loader = useCallback(
    () =>
      getSalesOverTime(token, {
        from: range.from,
        to: range.to,
        granularity: resolvedGranularity,
      }),
    [token, range.from, range.to, resolvedGranularity]
  );
  const { data, error, loading, reload } = useAsync(loader, refreshKey);

  const points = data ? toTrendPoints(data, metric) : [];
  const isMoney = metric === 'revenue';
  const hasSales = data?.points.some((p) => p.orderCount > 0);

  return (
    <DashboardSection
      id="revenue-trend"
      title="Revenue trend"
      description="Confirmed, preparing, ready and completed orders by UTC period."
      loading={loading}
      error={error}
      onRetry={reload}
      hasData={Boolean(data)}
      isEmpty={!hasSales}
      emptyTitle="No sales in this period."
      controls={
        <>
          <div className="field">
            <label htmlFor="trend-metric">Measure</label>
            <select id="trend-metric" value={metric} onChange={(e) => setMetric(e.target.value)}>
              {Object.entries(TREND_METRICS).map(([key, { label }]) => (
                <option key={key} value={key}>{label}</option>
              ))}
            </select>
          </div>
          <div className="field">
            <label htmlFor="trend-granularity">Group by</label>
            <select
              id="trend-granularity"
              value={granularity}
              onChange={(e) => setGranularity(e.target.value)}
            >
              <option value="auto">Automatic</option>
              <option value={GRANULARITY.DAY} disabled={range.days > 366}>Day</option>
              <option value={GRANULARITY.MONTH}>Month</option>
            </select>
          </div>
        </>
      }
    >
      <LineChart
        title={`${TREND_METRICS[metric].label} over time`}
        points={points}
        formatValue={isMoney ? formatMoney : formatNumber}
        formatTick={isMoney ? formatCompact : formatNumber}
      />
    </DashboardSection>
  );
}
