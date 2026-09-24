import { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useMemberOneApi } from '../../hooks/useMemberOneApi';
import {
  buildInventorySummary,
  buildStockAdjustmentPayload,
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
  normalizeInventoryItems,
  normalizeStockHistoryItems,
  STOCK_TRANSACTION_TYPES,
  validateStockAdjustment,
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

export function InventoryDashboardPage() {
  const api = useMemberOneApi();
  const [inventoryItems, setInventoryItems] = useState([]);
  const [lowStockItems, setLowStockItems] = useState([]);
  const [selectedVariantId, setSelectedVariantId] = useState(null);
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
      setError(normalizeError(err));
    } finally {
      setIsLoadingSelected(false);
    }
  }, [api]);

  useEffect(() => {
    // This effect intentionally loads dashboard data when the authenticated API
    // client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadDashboard();
  }, [loadDashboard]);

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
      setError(normalizeError(err));
    } finally {
      setIsSubmittingAdjustment(false);
    }
  }

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
      {message && <Alert>{message}</Alert>}

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
                  <dt>Reorder level</dt>
                  <dd>{getReorderLevel(selectedInventoryItem)}</dd>
                </div>
              </dl>

              <Alert>
                Stock-changing actions are submitted to the backend inventory service. The frontend does not calculate or overwrite stock.
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
