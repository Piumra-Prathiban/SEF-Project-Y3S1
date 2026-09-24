import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';
import Badge from '../../../../components/Badge';
import DashboardSection from '../../../../components/dashboard/DashboardSection';
import { useAsync } from '../../../../hooks/useAsync';
import { getPromotionPerformance } from '../../../../services/analyticsService';
import { formatDiscount, formatMoney } from '../../marketingUtils';
import { formatNumber } from '../analyticsUtils';

export default function PromotionPerformanceSection({ token, range, refreshKey }) {
  const [liveOnly, setLiveOnly] = useState(false);
  const [sortBy, setSortBy] = useState('redemptions');

  const loader = useCallback(
    () =>
      getPromotionPerformance(token, {
        from: range.from,
        to: range.to,
        liveOnly: liveOnly || undefined,
        sortBy,
        pageSize: 20,
      }),
    [token, range.from, range.to, liveOnly, sortBy]
  );
  const { data, error, loading, reload } = useAsync(loader, refreshKey);

  return (
    <DashboardSection
      id="promotion-performance"
      title="Promotion performance"
      description="Coupon redemptions in the period and the revenue of the orders they were used on."
      loading={loading}
      error={error}
      onRetry={reload}
      hasData={Boolean(data)}
      isEmpty={data?.items.length === 0}
      emptyTitle={liveOnly ? 'No promotions are live right now.' : 'No promotions yet.'}
      controls={
        <>
          <div className="field">
            <label htmlFor="promotion-sort">Sort by</label>
            <select id="promotion-sort" value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
              <option value="redemptions">Redemptions</option>
              <option value="revenue">Order revenue</option>
              <option value="discountAmount">Discount given</option>
              <option value="name">Name</option>
            </select>
          </div>
          <div className="field-inline">
            <input
              id="promotion-live-only"
              type="checkbox"
              checked={liveOnly}
              onChange={(e) => setLiveOnly(e.target.checked)}
            />
            <label htmlFor="promotion-live-only">Live only</label>
          </div>
        </>
      }
    >
      {data && (
        <>
          <ul className="mini-stats">
            <li>
              <span className="mini-stat-value">{data.livePromotionCount}</span>
              <span className="mini-stat-label">Live promotions now</span>
            </li>
            <li>
              <span className="mini-stat-value">{formatNumber(data.totalRedemptions)}</span>
              <span className="mini-stat-label">Coupon redemptions in period</span>
            </li>
          </ul>

          <div className="table-wrap">
            <table>
              <caption className="visually-hidden">Promotion performance</caption>
              <thead>
                <tr>
                  <th scope="col">Promotion</th>
                  <th scope="col">Offer</th>
                  <th scope="col">Status</th>
                  <th scope="col" className="numeric">Redemptions</th>
                  <th scope="col" className="numeric">Customers</th>
                  <th scope="col" className="numeric">Orders</th>
                  <th scope="col" className="numeric">Order revenue</th>
                  <th scope="col" className="numeric">Discount given</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((item) => (
                  <tr key={item.promotionId}>
                    <td>
                      <Link to={`/marketing/promotions/${item.promotionId}`}>{item.name}</Link>
                      {item.campaignName && <span className="muted"> · {item.campaignName}</span>}
                    </td>
                    <td>{formatDiscount({ type: item.type, discountValue: item.discountValue })}</td>
                    <td>
                      <Badge tone={item.isLive ? 'success' : 'muted'}>
                        {item.isLive ? 'Live' : 'Not live'}
                      </Badge>
                    </td>
                    <td className="numeric">{formatNumber(item.redemptions)}</td>
                    <td className="numeric">{formatNumber(item.uniqueCustomers)}</td>
                    <td className="numeric">{formatNumber(item.redeemedOrderCount)}</td>
                    <td className="numeric">{formatMoney(item.redeemedOrderRevenue)}</td>
                    <td className="numeric">{formatMoney(item.discountAmount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="data-note">
            Only coupon-based promotions can be measured: orders do not record automatic
            promotions, and checkout does not yet store discount amounts.
          </p>
        </>
      )}
    </DashboardSection>
  );
}
