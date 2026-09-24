import { useCallback, useState } from 'react';
import BarList from '../../../../components/charts/BarList';
import DashboardSection from '../../../../components/dashboard/DashboardSection';
import { useAsync } from '../../../../hooks/useAsync';
import { getProductPerformance } from '../../../../services/analyticsService';
import { formatMoney } from '../../marketingUtils';
import { formatNumber } from '../analyticsUtils';

const MODES = {
  top: {
    id: 'top-products',
    title: 'Top selling products',
    description: 'Highest sellers in the period.',
    sortDirection: 'desc',
  },
  low: {
    id: 'low-products',
    title: 'Low performing products',
    description: 'Lowest sellers in the period, including products with no sales.',
    sortDirection: 'asc',
  },
};

export default function ProductPerformanceSection({ token, range, refreshKey, mode = 'top', limit = 10 }) {
  const config = MODES[mode];
  const [metric, setMetric] = useState('unitsSold');

  const loader = useCallback(
    () =>
      getProductPerformance(token, {
        from: range.from,
        to: range.to,
        sortBy: metric,
        sortDirection: config.sortDirection,
        pageSize: limit,
      }),
    [token, range.from, range.to, metric, config.sortDirection, limit]
  );
  const { data, error, loading, reload } = useAsync(loader, refreshKey);

  const items = (data?.items ?? []).map((product) => ({
    key: product.productId,
    label: product.productName,
    value: metric === 'revenue' ? product.revenue : product.unitsSold,
    valueLabel:
      metric === 'revenue'
        ? formatMoney(product.revenue)
        : `${formatNumber(product.unitsSold)} units`,
  }));

  // "Top sellers" with no sales at all is not a ranking worth showing.
  const isEmpty = mode === 'top'
    ? items.every((item) => item.value === 0)
    : items.length === 0;

  return (
    <DashboardSection
      id={config.id}
      title={config.title}
      description={config.description}
      loading={loading}
      error={error}
      onRetry={reload}
      hasData={Boolean(data)}
      isEmpty={isEmpty}
      emptyTitle={mode === 'top' ? 'No products sold in this period.' : 'No products to show.'}
      controls={
        <div className="field">
          <label htmlFor={`${config.id}-metric`}>Rank by</label>
          <select id={`${config.id}-metric`} value={metric} onChange={(e) => setMetric(e.target.value)}>
            <option value="unitsSold">Units sold</option>
            <option value="revenue">Revenue</option>
          </select>
        </div>
      }
    >
      <BarList title={config.title} items={items} />
    </DashboardSection>
  );
}
