import { useCallback, useState } from 'react';
import Badge from '../../../../components/Badge';
import DashboardSection from '../../../../components/dashboard/DashboardSection';
import { useAsync } from '../../../../hooks/useAsync';
import { getDemandInsights } from '../../../../services/analyticsService';
import { DEMAND_TRENDS, formatNumber, formatPercent } from '../analyticsUtils';

const SORTS = {
  high: { label: 'Highest demand', sortBy: 'unitsSold', sortDirection: 'desc' },
  low: { label: 'Lowest demand', sortBy: 'unitsSold', sortDirection: 'asc' },
  rising: { label: 'Fastest rising', sortBy: 'trend', sortDirection: 'desc' },
  falling: { label: 'Fastest falling', sortBy: 'trend', sortDirection: 'asc' },
};

export function DemandTrendBadge({ trend }) {
  const info = DEMAND_TRENDS[trend] ?? DEMAND_TRENDS[0];

  return (
    <Badge tone={info.tone}>
      <span aria-hidden="true">{info.icon} </span>
      {info.label}
    </Badge>
  );
}

export default function DemandSection({ token, range, refreshKey }) {
  const [sort, setSort] = useState('high');

  const loader = useCallback(
    () =>
      getDemandInsights(token, {
        from: range.from,
        to: range.to,
        sortBy: SORTS[sort].sortBy,
        sortDirection: SORTS[sort].sortDirection,
        pageSize: 10,
      }),
    [token, range.from, range.to, sort]
  );
  const { data, error, loading, reload } = useAsync(loader, refreshKey);

  return (
    <DashboardSection
      id="demand-insights"
      title="Demand insights"
      description="Sales velocity per variant, compared with the previous period of the same length."
      loading={loading}
      error={error}
      onRetry={reload}
      hasData={Boolean(data)}
      isEmpty={data?.items.length === 0}
      emptyTitle="No active product variants."
      controls={
        <div className="field">
          <label htmlFor="demand-sort">Show</label>
          <select id="demand-sort" value={sort} onChange={(e) => setSort(e.target.value)}>
            {Object.entries(SORTS).map(([key, option]) => (
              <option key={key} value={key}>{option.label}</option>
            ))}
          </select>
        </div>
      }
    >
      {data && (
        <>
          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Demand by variant</caption>
              <thead>
                <tr>
                  <th scope="col">Product</th>
                  <th scope="col">SKU</th>
                  <th scope="col" className="numeric">Units sold</th>
                  <th scope="col" className="numeric">Previous period</th>
                  <th scope="col" className="numeric">Units / day</th>
                  <th scope="col">Trend</th>
                  <th scope="col" className="numeric">Change</th>
                  <th scope="col" className="numeric">Days of cover</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((item) => (
                  <tr key={item.productVariantId}>
                    <td>{item.productName}</td>
                    <td>{item.sku}</td>
                    <td className="numeric">{formatNumber(item.unitsSold)}</td>
                    <td className="numeric">{formatNumber(item.previousUnitsSold)}</td>
                    <td className="numeric">{item.unitsPerDay}</td>
                    <td><DemandTrendBadge trend={item.trend} /></td>
                    <td className="numeric">{formatPercent(item.trendPercent)}</td>
                    <td className="numeric">{item.daysOfCover ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="data-note">
            Demand is measured from recorded sales, so products that were out of stock may
            show lower demand than customers actually had.
          </p>
        </>
      )}
    </DashboardSection>
  );
}
