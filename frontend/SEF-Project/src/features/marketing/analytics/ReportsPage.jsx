import { useCallback, useState } from 'react';
import PageHeader from '../../../components/PageHeader';
import { ErrorState, LoadingState } from '../../../components/StatusViews';
import { useAuth } from '../../../contexts/AuthContext';
import { useAsync } from '../../../hooks/useAsync';
import {
  getDemandInsights,
  getInventoryStock,
  getProductPerformance,
  getPromotionPerformance,
  getSalesSummary,
} from '../../../services/analyticsService';
import { formatMoney } from '../marketingUtils';
import {
  describeRange,
  formatNumber,
  formatPercent,
  percentChange,
  previousRange,
  STOCK_STATUS,
} from './analyticsUtils';
import DateRangeFilter from './DateRangeFilter';
import { DemandTrendBadge } from './sections/DemandSection';
import { useDateRange } from './useDateRange';

const TREND_RISING = 2;
const TREND_FALLING = 4;

// Written summaries built only from API responses; nothing is estimated here.
export default function ReportsPage() {
  const { token } = useAuth();
  const { selection, apiRange, setSelection } = useDateRange();
  const [refreshKey, setRefreshKey] = useState(0);

  const loader = useCallback(async () => {
    const range = { from: apiRange.from, to: apiRange.to };
    const [
      sales,
      previousSales,
      topProducts,
      lowProducts,
      outOfStock,
      lowStock,
      promotions,
      demand,
    ] = await Promise.all([
      getSalesSummary(token, range),
      getSalesSummary(token, previousRange(apiRange)),
      getProductPerformance(token, { ...range, sortBy: 'revenue', sortDirection: 'desc', pageSize: 5 }),
      getProductPerformance(token, { ...range, sortBy: 'unitsSold', sortDirection: 'asc', pageSize: 100 }),
      getInventoryStock(token, { stockStatus: STOCK_STATUS.OUT_OF_STOCK, pageSize: 100 }),
      getInventoryStock(token, { stockStatus: STOCK_STATUS.LOW_STOCK, pageSize: 100 }),
      getPromotionPerformance(token, { ...range, sortBy: 'redemptions', pageSize: 100 }),
      getDemandInsights(token, { ...range, sortBy: 'trend', sortDirection: 'desc', pageSize: 100 }),
    ]);

    return { sales, previousSales, topProducts, lowProducts, outOfStock, lowStock, promotions, demand };
  }, [token, apiRange]);

  const { data, error, loading, reload } = useAsync(loader, refreshKey);

  return (
    <>
      <PageHeader
        title="Reports"
        subtitle="Business summaries for the selected period."
        actions={
          <button type="button" className="button button-secondary" onClick={() => window.print()} disabled={!data}>
            Print
          </button>
        }
      />

      <DateRangeFilter
        selection={selection}
        apiRange={apiRange}
        onChange={setSelection}
        onRefresh={() => setRefreshKey((key) => key + 1)}
        refreshing={loading}
      />

      {error && <ErrorState error={error} onRetry={reload} />}
      {!error && !data && <LoadingState label="Preparing reports…" />}
      {!error && data && (
        <div className={loading ? 'reports is-refreshing' : 'reports'} aria-busy={loading}>
          <SalesReport sales={data.sales} previous={data.previousSales} />
          <ProductReport top={data.topProducts.items} all={data.lowProducts.items} hasSales={data.sales.orderCount > 0} />
          <InventoryReport outOfStock={data.outOfStock} lowStock={data.lowStock} />
          <PromotionReport promotions={data.promotions} />
          <DemandReport items={data.demand.items} />
        </div>
      )}
    </>
  );
}

function ReportCard({ id, title, children }) {
  return (
    <article className="report" aria-labelledby={id}>
      <h2 id={id}>{title}</h2>
      {children}
    </article>
  );
}

function SalesReport({ sales, previous }) {
  const period = describeRange(sales);

  if (sales.orderCount === 0) {
    return (
      <ReportCard id="report-sales" title="Sales summary">
        <p>No sales were recorded between {period}.</p>
      </ReportCard>
    );
  }

  const revenueChange = previous.orderCount > 0
    ? percentChange(sales.netRevenue, previous.netRevenue)
    : null;

  return (
    <ReportCard id="report-sales" title="Sales summary">
      <p>
        Between {period}, <strong>{formatNumber(sales.orderCount)} orders</strong> generated{' '}
        <strong>{formatMoney(sales.netRevenue)}</strong> in revenue, selling{' '}
        {formatNumber(sales.unitsSold)} units at an average of {formatMoney(sales.averageOrderValue)} per order.
      </p>
      <p>
        {revenueChange === null
          ? 'There were no sales in the previous period of the same length to compare with.'
          : `Revenue changed by ${formatPercent(revenueChange)} compared with the previous period (${formatMoney(previous.netRevenue)} from ${formatNumber(previous.orderCount)} orders).`}
      </p>
      {sales.discountTotal > 0 && (
        <p>Discounts of {formatMoney(sales.discountTotal)} were applied to these orders.</p>
      )}
    </ReportCard>
  );
}

function ProductReport({ top, all, hasSales }) {
  const sold = top.filter((product) => product.unitsSold > 0);
  const unsold = all.filter((product) => product.unitsSold === 0);

  return (
    <ReportCard id="report-products" title="Product performance">
      {!hasSales || sold.length === 0 ? (
        <p>No products were sold in this period.</p>
      ) : (
        <>
          <p>Best sellers by revenue:</p>
          <ol>
            {sold.map((product) => (
              <li key={product.productId}>
                {product.productName}: {formatMoney(product.revenue)} from{' '}
                {formatNumber(product.unitsSold)} units
              </li>
            ))}
          </ol>
        </>
      )}
      {unsold.length > 0 && (
        <p>
          <strong>{unsold.length}</strong> active {unsold.length === 1 ? 'product' : 'products'} had
          no sales: {unsold.map((product) => product.productName).join(', ')}.
        </p>
      )}
    </ReportCard>
  );
}

function InventoryReport({ outOfStock, lowStock }) {
  const nothingToReport = outOfStock.totalItems === 0 && lowStock.totalItems === 0;

  return (
    <ReportCard id="report-inventory" title="Inventory status (current)">
      {nothingToReport ? (
        <p>All active variants are above their reorder level.</p>
      ) : (
        <>
          <StockList title="Out of stock" page={outOfStock} />
          <StockList title="Low stock (at or below reorder level)" page={lowStock} />
        </>
      )}
    </ReportCard>
  );
}

function StockList({ title, page }) {
  if (page.totalItems === 0) {
    return null;
  }

  return (
    <>
      <p>
        {title}: <strong>{page.totalItems}</strong>
      </p>
      <ul>
        {page.items.map((item) => (
          <li key={item.productVariantId}>
            {item.productName} {item.variantName} ({item.sku}): {formatNumber(item.availableQuantity)} available,
            reorder level {formatNumber(item.reorderLevel)}
          </li>
        ))}
      </ul>
    </>
  );
}

function PromotionReport({ promotions }) {
  const used = promotions.items.filter((item) => item.redemptions > 0);

  return (
    <ReportCard id="report-promotions" title="Promotions">
      <p>
        <strong>{promotions.livePromotionCount}</strong>{' '}
        {promotions.livePromotionCount === 1 ? 'promotion is' : 'promotions are'} live right now.
        Coupons were redeemed {formatNumber(promotions.totalRedemptions)} times in this period.
      </p>
      {used.length > 0 && (
        <ul>
          {used.map((item) => (
            <li key={item.promotionId}>
              {item.name}: {formatNumber(item.redemptions)} redemptions by{' '}
              {formatNumber(item.uniqueCustomers)} customers, {formatMoney(item.redeemedOrderRevenue)} in order revenue
            </li>
          ))}
        </ul>
      )}
      <p className="data-note">Only coupon-based promotions can be measured with the current data.</p>
    </ReportCard>
  );
}

function DemandReport({ items }) {
  const rising = items.filter((item) => item.trend === TREND_RISING);
  const falling = items.filter((item) => item.trend === TREND_FALLING);

  return (
    <ReportCard id="report-demand" title="Demand trends">
      {rising.length === 0 && falling.length === 0 ? (
        <p>No variant changed demand by 10% or more compared with the previous period.</p>
      ) : (
        <ul className="plain-list">
          {[...rising, ...falling].map((item) => (
            <li key={item.productVariantId}>
              <DemandTrendBadge trend={item.trend} /> {item.productName} ({item.sku}):{' '}
              {formatNumber(item.previousUnitsSold)} → {formatNumber(item.unitsSold)} units (
              {formatPercent(item.trendPercent)})
            </li>
          ))}
        </ul>
      )}
    </ReportCard>
  );
}
