import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import {
  buildInventorySummary,
  getColourName,
  getInventoryQuantity,
  getInventoryStatus,
  getProductName,
  getReorderLevel,
  getSizeName,
  getSku,
  normalizeInventoryItems,
} from './inventoryDashboardUtils';

function normalizeError(error) {
  if (error?.errors) {
    return Object.values(error.errors).flat().join(' ');
  }

  return error?.detail || error?.message || 'Something went wrong.';
}

function getStatusClass(status) {
  const normalized = status.toLowerCase();

  if (normalized.includes('out')) {
    return 'is-danger';
  }

  if (normalized.includes('low')) {
    return 'is-warning';
  }

  return 'is-active';
}

export function InventoryDashboardPage() {
  const api = useMemberOneApi();
  const [inventoryItems, setInventoryItems] = useState([]);
  const [lowStockItems, setLowStockItems] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [lastRefreshedAt, setLastRefreshedAt] = useState(null);

  const summary = useMemo(
    () => buildInventorySummary(inventoryItems, lowStockItems),
    [inventoryItems, lowStockItems],
  );

  const loadDashboard = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [inventoryResponse, lowStockResponse] = await Promise.all([
        api.getInventory(),
        api.getLowStock(),
      ]);

      setInventoryItems(normalizeInventoryItems(inventoryResponse));
      setLowStockItems(normalizeInventoryItems(lowStockResponse));
      setLastRefreshedAt(new Date());
    } catch (err) {
      setError(normalizeError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads dashboard data when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDashboard();
  }, [loadDashboard]);

  return (
    <PageShell
      eyebrow="Member 1"
      title="Inventory Dashboard"
      description="Monitor current stock, reorder levels and low-stock risk across product variants."
    >
      <div className="toolbar">
        <div>
          {lastRefreshedAt && (
            <p className="muted-text">
              Last refreshed {lastRefreshedAt.toLocaleTimeString()}
            </p>
          )}
        </div>
        <button
          disabled={isLoading}
          onClick={loadDashboard}
          type="button"
        >
          {isLoading ? 'Refreshing...' : 'Refresh inventory'}
        </button>
      </div>

      {error && <Alert tone="danger">{error}</Alert>}

      {isLoading ? (
        <LoadingState message="Loading inventory dashboard..." />
      ) : (
        <>
          <section className="summary-grid" aria-label="Inventory summary">
            <article className="summary-card">
              <span>Total products</span>
              <strong>{summary.totalProducts}</strong>
            </article>
            <article className="summary-card">
              <span>Total variants</span>
              <strong>{summary.totalVariants}</strong>
            </article>
            <article className="summary-card">
              <span>Total stock</span>
              <strong>{summary.totalStock}</strong>
            </article>
            <article className="summary-card">
              <span>Low-stock variants</span>
              <strong>{summary.lowStockVariants}</strong>
            </article>
            <article className="summary-card">
              <span>Out-of-stock variants</span>
              <strong>{summary.outOfStockVariants}</strong>
            </article>
          </section>

          {inventoryItems.length === 0 ? (
            <div className="empty-state">
              No inventory records were returned by the API.
            </div>
          ) : (
            <div className="table-card">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Product</th>
                    <th>SKU</th>
                    <th>Size</th>
                    <th>Colour</th>
                    <th>Current stock</th>
                    <th>Reorder level</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {inventoryItems.map((item, index) => {
                    const status = getInventoryStatus(item);

                    return (
                      <tr key={item.id ?? item.productVariantId ?? item.variantId ?? index}>
                        <td>{getProductName(item)}</td>
                        <td>{getSku(item)}</td>
                        <td>{getSizeName(item)}</td>
                        <td>{getColourName(item)}</td>
                        <td>{getInventoryQuantity(item)}</td>
                        <td>{getReorderLevel(item)}</td>
                        <td>
                          <span className={`status-pill ${getStatusClass(status)}`}>
                            {status}
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </PageShell>
  );
}
