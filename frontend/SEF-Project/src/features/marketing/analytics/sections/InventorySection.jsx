import { useCallback, useState } from 'react';
import Badge from '../../../../components/Badge';
import DashboardSection from '../../../../components/dashboard/DashboardSection';
import Pagination from '../../../../components/Pagination';
import { useAsync } from '../../../../hooks/useAsync';
import { getInventoryStock, getInventorySummary } from '../../../../services/analyticsService';
import { formatNumber, STOCK_STATUS_LABELS } from '../analyticsUtils';

const PAGE_SIZE = 10;

export default function InventorySection({ token, refreshKey }) {
  const [stockStatus, setStockStatus] = useState('');
  const [page, setPage] = useState(1);

  const loader = useCallback(async () => {
    const [summary, stock] = await Promise.all([
      getInventorySummary(token),
      getInventoryStock(token, { stockStatus, page, pageSize: PAGE_SIZE }),
    ]);
    return { summary, stock };
  }, [token, stockStatus, page]);
  const { data, error, loading, reload } = useAsync(loader, refreshKey);

  return (
    <DashboardSection
      id="inventory-insights"
      title="Inventory insights"
      description="Current stock for active variants. Available = on hand − reserved. Not affected by the period."
      loading={loading}
      error={error}
      onRetry={reload}
      hasData={Boolean(data)}
      isEmpty={data?.summary.variantCount === 0}
      emptyTitle="No active product variants."
      controls={
        <div className="field">
          <label htmlFor="stock-status">Stock status</label>
          <select
            id="stock-status"
            value={stockStatus}
            onChange={(e) => {
              setStockStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="">All (lowest stock first)</option>
            {STOCK_STATUS_LABELS.map((status, value) => (
              <option key={status.label} value={value}>{status.label}</option>
            ))}
          </select>
        </div>
      }
    >
      {data && (
        <>
          <ul className="mini-stats">
            <MiniStat label="Out of stock" value={data.summary.outOfStockCount} />
            <MiniStat label="Low stock" value={data.summary.lowStockCount} />
            <MiniStat label="In stock" value={data.summary.inStockCount} />
            <MiniStat label="Units on hand" value={formatNumber(data.summary.totalQuantityOnHand)} />
          </ul>

          {data.stock.items.length === 0 ? (
            <p className="muted">No variants with this status.</p>
          ) : (
            <>
              <div className="table-wrap">
                <table>
                  <caption className="visually-hidden">Stock by variant</caption>
                  <thead>
                    <tr>
                      <th scope="col">Product</th>
                      <th scope="col">SKU</th>
                      <th scope="col" className="numeric">Available</th>
                      <th scope="col" className="numeric">Reserved</th>
                      <th scope="col" className="numeric">Reorder level</th>
                      <th scope="col">Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.stock.items.map((item) => {
                      const status = STOCK_STATUS_LABELS[item.stockStatus];

                      return (
                        <tr key={item.productVariantId}>
                          <td>{item.productName} <span className="muted">{item.variantName}</span></td>
                          <td>{item.sku}</td>
                          <td className="numeric">{formatNumber(item.availableQuantity)}</td>
                          <td className="numeric">{formatNumber(item.reservedQuantity)}</td>
                          <td className="numeric">{formatNumber(item.reorderLevel)}</td>
                          <td><Badge tone={status.tone}>{status.label}</Badge></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              <Pagination
                page={data.stock.page}
                pageSize={data.stock.pageSize}
                totalCount={data.stock.totalItems}
                onPageChange={setPage}
              />
            </>
          )}
        </>
      )}
    </DashboardSection>
  );
}

function MiniStat({ label, value }) {
  return (
    <li>
      <span className="mini-stat-value">{value}</span>
      <span className="mini-stat-label">{label}</span>
    </li>
  );
}
