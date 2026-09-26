import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useCatalogApi } from '../../hooks/useCatalogApi';
import { normalizeApiError } from '../../utils/apiErrorUtils';
import {
  buildInventorySummary,
  buildStockAdjustmentPayload,
  defaultInventoryQuery,
  getColourName,
  getHistoryDate,
  getHistoryNewQuantity,
  getHistoryPreviousQuantity,
  getHistoryQuantity,
  getHistoryReason,
  getHistoryResponsibleUser,
  getHistoryType,
  getInventoryQuantity,
  getInventoryStatus,
  getProductName,
  getReorderLevel,
  getSizeName,
  getSku,
  getVariantId,
  filterInventoryItems,
  normalizeInventoryItems,
  normalizeStockHistoryItems,
  paginateItems,
  STOCK_TRANSACTION_TYPES,
  updateInventoryQuery,
  validateStockAdjustment,
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

function formatHistoryDate(value) {
  if (!value) {
    return '-';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

const initialAdjustmentForm = {
  transactionType: 'StockIn',
  quantity: '',
  reason: '',
};

function getInitialSelectedVariantId() {
  return new URLSearchParams(window.location.search).get('variantId');
}

export function InventoryDashboardPage() {
  const api = useCatalogApi();
  const [allInventoryItems, setAllInventoryItems] = useState([]);
  const [lowStockItems, setLowStockItems] = useState([]);
  const [categories, setCategories] = useState([]);
  const [query, setQuery] = useState(defaultInventoryQuery);
  const [selectedVariantId, setSelectedVariantId] = useState(getInitialSelectedVariantId);
  const [selectedInventoryItem, setSelectedInventoryItem] = useState(null);
  const [stockHistory, setStockHistory] = useState([]);
  const [adjustmentForm, setAdjustmentForm] = useState(initialAdjustmentForm);
  const [adjustmentErrors, setAdjustmentErrors] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingSelected, setIsLoadingSelected] = useState(false);
  const [isSubmittingAdjustment, setIsSubmittingAdjustment] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [lastRefreshedAt, setLastRefreshedAt] = useState(null);

  const filteredInventory = useMemo(
    () => filterInventoryItems(allInventoryItems, query),
    [allInventoryItems, query],
  );
  const paginationMeta = useMemo(
    () => paginateItems(filteredInventory, query.page, query.pageSize),
    [filteredInventory, query.page, query.pageSize],
  );
  const inventoryItems = paginationMeta.items;

  const summary = useMemo(
    () => buildInventorySummary(filteredInventory, lowStockItems),
    [filteredInventory, lowStockItems],
  );

  const loadDashboard = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const [inventoryResponse, lowStockResponse] = await Promise.all([
        api.getInventory(),
        api.getLowStock(),
      ]);

      setAllInventoryItems(normalizeInventoryItems(inventoryResponse));
      setLowStockItems(normalizeInventoryItems(lowStockResponse));
      setLastRefreshedAt(new Date());
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsLoading(false);
    }
  }, [api]);

  const loadLookups = useCallback(async () => {
    try {
      const categoryResponse = await api.getCategories();
      setCategories(categoryResponse);
    } catch (err) {
      setError(normalizeApiError(err));
    }
  }, [api]);

  const loadSelectedVariant = useCallback(async (variantId, fallbackItem) => {
    if (!variantId) {
      setSelectedInventoryItem(null);
      setStockHistory([]);
      return;
    }

    setIsLoadingSelected(true);
    setError(null);

    try {
      const [inventoryItemResponse, historyResponse] = await Promise.all([
        api.getInventoryItem(variantId),
        api.getStockHistory(variantId),
      ]);

      setSelectedInventoryItem(inventoryItemResponse ?? fallbackItem);
      setStockHistory(normalizeStockHistoryItems(historyResponse));
    } catch (err) {
      setSelectedInventoryItem(fallbackItem ?? null);
      setStockHistory([]);
      setError(normalizeApiError(err));
    } finally {
      setIsLoadingSelected(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads dashboard data when the authenticated API
    // client or inventory query changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDashboard();
  }, [loadDashboard]);

  useEffect(() => {
    if (!selectedVariantId || selectedInventoryItem) {
      return;
    }

    const matchingItem = allInventoryItems.find(
      (item) => String(getVariantId(item)) === String(selectedVariantId),
    );

    // This effect intentionally opens the variant linked from the low-stock
    // page once inventory data is available.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadSelectedVariant(selectedVariantId, matchingItem);
  }, [allInventoryItems, loadSelectedVariant, selectedInventoryItem, selectedVariantId]);

  useEffect(() => {
    // This effect intentionally loads category filter data when the
    // authenticated API client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLookups();
  }, [loadLookups]);

  function updateQuery(field, value) {
    setQuery((current) => updateInventoryQuery(current, field, value));
  }

  function updateAdjustmentField(field, value) {
    setAdjustmentForm((current) => ({
      ...current,
      [field]: value,
    }));
  }

  function handleSelectInventoryItem(item) {
    const variantId = getVariantId(item);

    setSelectedVariantId(variantId);
    setSelectedInventoryItem(item);
    setAdjustmentForm(initialAdjustmentForm);
    setAdjustmentErrors([]);
    setMessage(null);

    loadSelectedVariant(variantId, item);
  }

  async function handleSubmitAdjustment(event) {
    event.preventDefault();

    const errors = validateStockAdjustment(adjustmentForm);
    setAdjustmentErrors(errors);

    if (errors.length > 0) {
      return;
    }

    if (!selectedVariantId) {
      setError('Select a product variant before adjusting stock.');
      return;
    }

    setIsSubmittingAdjustment(true);
    setError(null);
    setMessage(null);

    try {
      await api.adjustStock(
        selectedVariantId,
        buildStockAdjustmentPayload(adjustmentForm),
      );

      setMessage('Stock adjustment submitted successfully.');
      setAdjustmentForm(initialAdjustmentForm);
      await loadDashboard();
      await loadSelectedVariant(selectedVariantId, selectedInventoryItem);
    } catch (err) {
      setError(normalizeApiError(err));
    } finally {
      setIsSubmittingAdjustment(false);
    }
  }

  return (
    <PageShell
      eyebrow="Clothic · Inventory"
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

      <div className="toolbar">
        <div className="toolbar__filters">
          <input
            aria-label="Filter inventory by product"
            onChange={(event) => updateQuery('product', event.target.value)}
            placeholder="Product"
            value={query.product}
          />

          <input
            aria-label="Filter inventory by SKU"
            onChange={(event) => updateQuery('sku', event.target.value)}
            placeholder="SKU"
            value={query.sku}
          />

          <select
            aria-label="Filter inventory by category"
            onChange={(event) => updateQuery('categoryId', event.target.value)}
            value={query.categoryId}
          >
            <option value="">All categories</option>
            {categories.map((category) => (
              <option key={category.id} value={category.id}>
                {category.name}
              </option>
            ))}
          </select>

          <select
            aria-label="Filter inventory by stock status"
            onChange={(event) => updateQuery('stockStatus', event.target.value)}
            value={query.stockStatus}
          >
            <option value="">All stock statuses</option>
            <option value="In Stock">In Stock</option>
            <option value="Low Stock">Low Stock</option>
            <option value="Out of Stock">Out of Stock</option>
          </select>

          <select
            aria-label="Inventory page size"
            onChange={(event) => updateQuery('pageSize', Number(event.target.value))}
            value={query.pageSize}
          >
            <option value={10}>10 per page</option>
            <option value={25}>25 per page</option>
            <option value={50}>50 per page</option>
          </select>

          <label className="checkbox-field">
            <input
              checked={query.lowStockOnly}
              onChange={(event) => updateQuery('lowStockOnly', event.target.checked)}
              type="checkbox"
            />
            Low-stock only
          </label>
        </div>
      </div>

      <ApiErrorAlert message={error} onRetry={loadDashboard} />
      {message && <Alert>{message}</Alert>}

      {isLoading ? (
        <LoadingState message="Loading inventory dashboard..." />
      ) : (
        <>
          <section className="summary-grid" aria-label="Inventory summary">
            <article className="summary-card">
              <span>Matching products</span>
              <strong>{summary.totalProducts}</strong>
            </article>
            <article className="summary-card">
              <span>Total matching variants</span>
              <strong>{summary.totalVariants}</strong>
            </article>
            <article className="summary-card">
              <span>Matching stock</span>
              <strong>{summary.totalStock}</strong>
            </article>
            <article className="summary-card">
              <span>All low-stock variants</span>
              <strong>{summary.lowStockVariants}</strong>
            </article>
            <article className="summary-card">
              <span>Matching out-of-stock variants</span>
              <strong>{summary.outOfStockVariants}</strong>
            </article>
          </section>

          {error && !allInventoryItems.length ? null : inventoryItems.length === 0 ? (
            <div className="empty-state">
              No inventory records match these filters.
            </div>
          ) : (
            <div className="table-card">
              <table className="data-table">
                <caption className="table-caption">
                  Inventory records with current stock, reorder level, status and stock-management action
                </caption>
                <thead>
                  <tr>
                    <th>Product</th>
                    <th>SKU</th>
                    <th>Size</th>
                    <th>Colour</th>
                    <th>On hand</th>
                    <th>Reserved</th>
                    <th>Available</th>
                    <th>Reorder level</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {inventoryItems.map((item, index) => {
                    const status = getInventoryStatus(item);
                    const variantId = getVariantId(item);

                    return (
                      <tr
                        className={variantId === selectedVariantId ? 'is-selected-row' : ''}
                        key={item.id ?? item.productVariantId ?? item.variantId ?? index}
                      >
                        <td>{getProductName(item)}</td>
                        <td>{getSku(item)}</td>
                        <td>{getSizeName(item)}</td>
                        <td>{getColourName(item)}</td>
                        <td>{getInventoryQuantity(item)}</td>
                        <td>{item.reservedQuantity ?? 0}</td>
                        <td>{item.availableQuantity ?? Number(getInventoryQuantity(item)) - Number(item.reservedQuantity ?? 0)}</td>
                        <td>{getReorderLevel(item)}</td>
                        <td>
                          <span className={`status-pill ${getStatusClass(status)}`}>
                            {status}
                          </span>
                        </td>
                        <td>
                          <button
                            className="button-secondary"
                            disabled={!variantId}
                            onClick={() => handleSelectInventoryItem(item)}
                            type="button"
                          >
                            Manage stock
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          <nav className="pagination-bar" aria-label="Inventory pagination">
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
              {' '}({paginationMeta.totalItems} inventory records, page size {paginationMeta.pageSize})
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

          {selectedInventoryItem && (
            <section className="panel stock-management-panel">
              <div className="panel__header">
                <div>
                  <h2>Stock Management</h2>
                  <p>
                    Selected variant: {getProductName(selectedInventoryItem)}
                    {' '}— {getSku(selectedInventoryItem)}
                  </p>
                </div>
                {isLoadingSelected && <span className="muted-text">Loading details...</span>}
              </div>

              <dl className="detail-grid">
                <div>
                  <dt>Product</dt>
                  <dd>{getProductName(selectedInventoryItem)}</dd>
                </div>
                <div>
                  <dt>SKU</dt>
                  <dd>{getSku(selectedInventoryItem)}</dd>
                </div>
                <div>
                  <dt>Size</dt>
                  <dd>{getSizeName(selectedInventoryItem)}</dd>
                </div>
                <div>
                  <dt>Colour</dt>
                  <dd>{getColourName(selectedInventoryItem)}</dd>
                </div>
                <div>
                  <dt>Current stock</dt>
                  <dd>{getInventoryQuantity(selectedInventoryItem)}</dd>
                </div>
                <div>
                  <dt>Reserved for orders</dt>
                  <dd>{selectedInventoryItem.reservedQuantity ?? 0}</dd>
                </div>
                <div>
                  <dt>Available to sell</dt>
                  <dd>{selectedInventoryItem.availableQuantity ?? Number(getInventoryQuantity(selectedInventoryItem)) - Number(selectedInventoryItem.reservedQuantity ?? 0)}</dd>
                </div>
                <div>
                  <dt>Reorder level</dt>
                  <dd>{getReorderLevel(selectedInventoryItem)}</dd>
                </div>
              </dl>

              <Alert>
                Removing stock cannot reduce on-hand quantity below the amount reserved for orders.
              </Alert>

              <form className="entity-form" onSubmit={handleSubmitAdjustment}>
                {adjustmentErrors.length > 0 && (
                  <Alert tone="danger">
                    <ul className="error-list">
                      {adjustmentErrors.map((adjustmentError) => (
                        <li key={adjustmentError}>{adjustmentError}</li>
                      ))}
                    </ul>
                  </Alert>
                )}

                <div className="form-grid">
                  <label>
                    Adjustment type
                    <select
                      onChange={(event) => updateAdjustmentField('transactionType', event.target.value)}
                      required
                      value={adjustmentForm.transactionType}
                    >
                      {STOCK_TRANSACTION_TYPES.map((type) => (
                        <option key={type.value} value={type.value}>
                          {type.label}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label>
                    Quantity
                    <input
                      min="1"
                      onChange={(event) => updateAdjustmentField('quantity', event.target.value)}
                      required
                      step="1"
                      type="number"
                      value={adjustmentForm.quantity}
                    />
                  </label>
                </div>

                <label>
                  Reason
                  <textarea
                    onChange={(event) => updateAdjustmentField('reason', event.target.value)}
                    required
                    rows={3}
                    value={adjustmentForm.reason}
                  />
                </label>

                <div className="form-actions">
                  <button disabled={isSubmittingAdjustment} type="submit">
                    {isSubmittingAdjustment ? 'Submitting...' : 'Submit stock adjustment'}
                  </button>
                </div>
              </form>

              <section className="stock-history-section">
                <h2>Stock History</h2>
                {stockHistory.length === 0 ? (
                  <div className="empty-state">
                    No stock transactions were returned for this variant.
                  </div>
                ) : (
                  <div className="table-card">
                    <table className="data-table">
                      <caption className="table-caption">
                        Stock transaction history for the selected product variant
                      </caption>
                      <thead>
                        <tr>
                          <th>Date</th>
                          <th>Transaction type</th>
                          <th>Quantity</th>
                          <th>Previous quantity</th>
                          <th>New quantity</th>
                          <th>Reason</th>
                          <th>Responsible user</th>
                        </tr>
                      </thead>
                      <tbody>
                        {stockHistory.map((transaction, index) => (
                          <tr key={transaction.id ?? index}>
                            <td>{formatHistoryDate(getHistoryDate(transaction))}</td>
                            <td>{getHistoryType(transaction)}</td>
                            <td>{getHistoryQuantity(transaction)}</td>
                            <td>{getHistoryPreviousQuantity(transaction)}</td>
                            <td>{getHistoryNewQuantity(transaction)}</td>
                            <td>{getHistoryReason(transaction)}</td>
                            <td>{getHistoryResponsibleUser(transaction)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            </section>
          )}
        </>
      )}
    </PageShell>
  );
}
