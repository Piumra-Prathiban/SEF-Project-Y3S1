import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useCatalogApi } from '../../hooks/useCatalogApi';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import {
  defaultLowStockQuery,
  getColourName,
  getInventoryQuantity,
  getInventoryStatus,
  getProductName,
  getReorderLevel,
  getShortageAmount,
  getSizeName,
  getSku,
  getVariantId,
  normalizeInventoryItems,
  paginateItems,
  sortLowStockItems,
  updateLowStockQuery,
} from './inventoryDashboardUtils';

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

export function LowStockPage() {
  const api = useCatalogApi();
  const navigate = useNavigate();
  const [query, setQuery] = useState(defaultLowStockQuery);
  const [allItems, setAllItems] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [lastRefreshedAt, setLastRefreshedAt] = useState(null);

  const sortedItems = useMemo(() => sortLowStockItems(allItems, query), [allItems, query]);
  const paginationMeta = useMemo(() => paginateItems(sortedItems, query.page, query.pageSize), [sortedItems, query.page, query.pageSize]);
  const items = paginationMeta.items;

  const loadLowStock = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const lowStockResponse = await api.getLowStock();
      setAllItems(normalizeInventoryItems(lowStockResponse));
      setLastRefreshedAt(new Date());
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally reloads low-stock data when sorting or
    // pagination query parameters change.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLowStock();
  }, [loadLowStock]);

  function updateQuery(field, value) {
    setQuery((current) => updateLowStockQuery(current, field, value));
  }

  function openInventoryRecord(item) {
    const variantId = getVariantId(item);

    if (variantId) {
      navigate(`/inventory?variantId=${encodeURIComponent(variantId)}`);
    } else {
      navigate('/inventory');
    }
  }

  return (
    <PageShell
      eyebrow="Clothic · Inventory"
      title="Low Stock Monitoring"
      description="Review variants returned by the backend low-stock endpoint and navigate to inventory management when action is needed."
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
          onClick={loadLowStock}
          type="button"
        >
          {isLoading ? 'Refreshing...' : 'Refresh low stock'}
        </button>
      </div>

      <div className="toolbar">
        <div className="toolbar__filters">
          <select
            aria-label="Sort low-stock variants"
            onChange={(event) => updateQuery('sortBy', event.target.value)}
            value={query.sortBy}
          >
            <option value="product">Product</option>
            <option value="sku">SKU</option>
            <option value="quantity">Current quantity</option>
            <option value="reorderLevel">Reorder level</option>
            <option value="shortage">Shortage</option>
          </select>

          <select
            aria-label="Low-stock sort direction"
            onChange={(event) => updateQuery('sortDirection', event.target.value)}
            value={query.sortDirection}
          >
            <option value="asc">Ascending</option>
            <option value="desc">Descending</option>
          </select>

          <select
            aria-label="Low-stock page size"
            onChange={(event) => updateQuery('pageSize', Number(event.target.value))}
            value={query.pageSize}
          >
            <option value={10}>10 per page</option>
            <option value={25}>25 per page</option>
            <option value={50}>50 per page</option>
          </select>
        </div>
      </div>

      <ApiErrorAlert message={error} onRetry={loadLowStock} />

      {isLoading ? (
        <LoadingState message="Loading low-stock variants..." />
      ) : error && !allItems.length ? null : items.length === 0 ? (
        <div className="empty-state">
          No low-stock variants were returned by the API.
        </div>
      ) : (
        <>
          <div className="table-card">
            <table className="data-table">
              <caption className="table-caption">
                Low-stock and out-of-stock variants returned by the backend
              </caption>
              <thead>
                <tr>
                  <th>Product</th>
                  <th>SKU</th>
                  <th>Size</th>
                  <th>Colour</th>
                  <th>Current quantity</th>
                  <th>Reorder level</th>
                  <th>Shortage</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item, index) => {
                  const status = getInventoryStatus(item);

                  return (
                    <tr key={item.id ?? item.productVariantId ?? item.variantId ?? index}>
                      <td>{getProductName(item)}</td>
                      <td>{getSku(item)}</td>
                      <td>{getSizeName(item)}</td>
                      <td>{getColourName(item)}</td>
                      <td>{getInventoryQuantity(item)}</td>
                      <td>{getReorderLevel(item)}</td>
                      <td>{getShortageAmount(item)}</td>
                      <td>
                        <span className={`status-pill ${getStatusClass(status)}`}>
                          {status}
                        </span>
                      </td>
                      <td>
                        <button
                          className="button-secondary"
                          onClick={() => openInventoryRecord(item)}
                          type="button"
                        >
                          Open inventory
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <nav className="pagination-bar" aria-label="Low-stock pagination">
            <button
              className="button-secondary"
              disabled={paginationMeta.page <= 1}
              onClick={() => updateQuery('page', paginationMeta.page - 1)}
              type="button"
            >
              Previous
            </button>
            <span>
              Page {paginationMeta.page} of {paginationMeta.totalPages || 1}
              {' '}({paginationMeta.totalItems} low-stock records, page size {paginationMeta.pageSize})
            </span>
            <button
              className="button-secondary"
              disabled={paginationMeta.page >= paginationMeta.totalPages}
              onClick={() => updateQuery('page', paginationMeta.page + 1)}
              type="button"
            >
              Next
            </button>
          </nav>
        </>
      )}
    </PageShell>
  );
}
