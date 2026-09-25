import { useCallback, useEffect, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import { formatCurrency, formatDate } from '../../utils/format';
import { PurchaseOrderForm } from './PurchaseOrderForm';
import {
  canCancelPurchaseOrder,
  canReceivePurchaseOrder,
  canSubmitPurchaseOrder,
  describePurchaseOrderError,
  getPurchaseOrderStatusName,
  PURCHASE_ORDER_STATUS_OPTIONS,
  PurchaseOrderStatus,
} from './purchaseOrderUtils';
import {
  cancelPurchaseOrder,
  createPurchaseOrder,
  getPurchaseOrders,
  getSuppliers,
  getVariantOptions,
  receivePurchaseOrder,
  submitPurchaseOrder,
} from './purchaseOrderService';

const defaultQuery = {
  page: 1,
  pageSize: 10,
  supplierId: '',
  status: '',
};

function statusPillClass(status) {
  switch (status) {
    case PurchaseOrderStatus.Submitted:
      return 'is-warning';
    case PurchaseOrderStatus.Received:
      return 'is-active';
    case PurchaseOrderStatus.Cancelled:
      return 'is-danger';
    default:
      return 'is-inactive';
  }
}

function PurchaseOrdersPage() {
  const { token } = useAuth();
  const guardSessionExpiry = useSessionGuard();

  const [query, setQuery] = useState(defaultQuery);
  const [data, setData] = useState(null);
  const [suppliers, setSuppliers] = useState([]);
  const [variantOptions, setVariantOptions] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [busyOrderId, setBusyOrderId] = useState(null);
  const [error, setError] = useState(null);
  const [actionError, setActionError] = useState(null);
  const [message, setMessage] = useState(null);
  const [retryTick, setRetryTick] = useState(0);

  const loadPurchaseOrders = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await getPurchaseOrders(token, {
        page: query.page,
        pageSize: query.pageSize,
        supplierId: query.supplierId,
        status: query.status,
      });

      setData(response);
    } catch (err) {
      if (!guardSessionExpiry(err)) {
        setError(err);
      }
    } finally {
      setIsLoading(false);
    }
  }, [token, query, guardSessionExpiry]);

  const loadLookups = useCallback(async () => {
    try {
      const [supplierResponse, variantResponse] = await Promise.all([
        getSuppliers(token),
        getVariantOptions(token),
      ]);

      setSuppliers(supplierResponse ?? []);
      setVariantOptions(variantResponse ?? []);
    } catch (err) {
      if (!guardSessionExpiry(err)) {
        setActionError(describePurchaseOrderError(err));
      }
    }
  }, [token, guardSessionExpiry]);

  useEffect(() => {
    // This effect intentionally reloads the purchase-order list when the
    // authenticated token, filters or page change, or when the user retries.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadPurchaseOrders();
  }, [loadPurchaseOrders, retryTick]);

  useEffect(() => {
    // This effect intentionally loads supplier and variant lookups when the
    // authenticated API client changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadLookups();
  }, [loadLookups]);

  function updateQuery(field, value) {
    setQuery((current) => ({
      ...current,
      [field]: value,
      ...(field === 'page' ? {} : { page: 1 }),
    }));
  }

  function openForm() {
    setIsFormOpen(true);
    setMessage(null);
    setActionError(null);
  }

  function closeForm() {
    setIsFormOpen(false);
  }

  async function handleCreate(payload) {
    setIsSaving(true);
    setActionError(null);
    setMessage(null);

    try {
      const created = await createPurchaseOrder(token, payload);

      setMessage(`Purchase order ${created.orderNumber} created as a draft.`);
      closeForm();
      setQuery((current) => ({ ...current, page: 1 }));
      await loadPurchaseOrders();
    } catch (err) {
      if (!guardSessionExpiry(err)) {
        setActionError(describePurchaseOrderError(err, 'create'));
      }
    } finally {
      setIsSaving(false);
    }
  }

  async function runAction(order, action, confirmMessage, serviceCall, successMessage) {
    const confirmed = window.confirm(confirmMessage);

    if (!confirmed) {
      return;
    }

    setBusyOrderId(order.id);
    setActionError(null);
    setMessage(null);

    try {
      await serviceCall();

      setMessage(successMessage);
      await loadPurchaseOrders();
    } catch (err) {
      if (!guardSessionExpiry(err)) {
        setActionError(describePurchaseOrderError(err, action));
      }
    } finally {
      setBusyOrderId(null);
    }
  }

  function handleSubmit(order) {
    return runAction(
      order,
      'submit',
      `Submit purchase order ${order.orderNumber} to ${order.supplierName}? `
        + 'It can no longer be edited as a draft.',
      () => submitPurchaseOrder(token, order.id),
      `Purchase order ${order.orderNumber} submitted.`,
    );
  }

  function handleReceive(order) {
    return runAction(
      order,
      'receive',
      `Receive purchase order ${order.orderNumber}? `
        + 'This will increase stock for every line and cannot be undone.',
      () => receivePurchaseOrder(token, order.id),
      `Purchase order ${order.orderNumber} received and stock updated.`,
    );
  }

  function handleCancel(order) {
    return runAction(
      order,
      'cancel',
      `Cancel purchase order ${order.orderNumber}? This cannot be undone.`,
      () => cancelPurchaseOrder(token, order.id),
      `Purchase order ${order.orderNumber} cancelled.`,
    );
  }

  const purchaseOrders = data?.items ?? [];
  const activeSuppliers = suppliers.filter(
    (supplier) => supplier.isActive !== false,
  );
  const totalPages = data?.totalPages ?? 1;

  return (
    <PageShell
      eyebrow="Clothic · Procurement"
      title="Purchase Orders"
      description="Raise restock orders against suppliers, then submit and receive them to top up stock."
    >
      <div className="toolbar">
        <div className="toolbar__filters">
          <select
            aria-label="Filter by supplier"
            onChange={(event) => updateQuery('supplierId', event.target.value)}
            value={query.supplierId}
          >
            <option value="">All suppliers</option>
            {activeSuppliers.map((supplier) => (
              <option key={supplier.id} value={supplier.id}>
                {supplier.name}
              </option>
            ))}
          </select>

          <select
            aria-label="Filter by status"
            onChange={(event) => updateQuery('status', event.target.value)}
            value={query.status}
          >
            <option value="">All statuses</option>
            {PURCHASE_ORDER_STATUS_OPTIONS.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </div>

        <button type="button" onClick={openForm}>
          Create purchase order
        </button>
      </div>

      {message && <Alert>{message}</Alert>}
      <ApiErrorAlert
        message={actionError}
        onRetry={null}
      />

      {isFormOpen && (
        <section className="panel">
          <h2>Create purchase order</h2>
          <PurchaseOrderForm
            isSubmitting={isSaving}
            onCancel={closeForm}
            onSubmit={handleCreate}
            suppliers={activeSuppliers}
            variantOptions={variantOptions}
          />
        </section>
      )}

      {isLoading ? (
        <LoadingState message="Loading purchase orders..." />
      ) : error ? (
        <ApiErrorAlert
          message={describePurchaseOrderError(error)}
          onRetry={() => setRetryTick((tick) => tick + 1)}
        />
      ) : purchaseOrders.length === 0 ? (
        <div className="empty-state">
          No purchase orders found.
        </div>
      ) : (
        <>
          <div className="table-card">
            <table className="data-table" aria-label="Purchase orders">
              <caption className="table-caption">
                Purchase orders with supplier, status, expected date and totals
              </caption>
              <thead>
                <tr>
                  <th scope="col">Order number</th>
                  <th scope="col">Supplier</th>
                  <th scope="col">Status</th>
                  <th scope="col">Expected</th>
                  <th scope="col">Lines</th>
                  <th scope="col">Total</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {purchaseOrders.map((order) => (
                  <tr key={order.id}>
                    <td>{order.orderNumber}</td>
                    <td>{order.supplierName}</td>
                    <td>
                      <span className={`status-pill ${statusPillClass(order.status)}`}>
                        {getPurchaseOrderStatusName(order.status)}
                      </span>
                    </td>
                    <td>{order.expectedAt ? formatDate(order.expectedAt) : '-'}</td>
                    <td>{order.items?.length ?? 0}</td>
                    <td>{formatCurrency(order.total, 'LKR')}</td>
                    <td>
                      <div className="table-actions">
                        {canSubmitPurchaseOrder(order.status) && (
                          <button
                            aria-label={`Submit ${order.orderNumber}`}
                            className="button-secondary"
                            disabled={busyOrderId === order.id}
                            onClick={() => handleSubmit(order)}
                            type="button"
                          >
                            Submit
                          </button>
                        )}

                        {canReceivePurchaseOrder(order.status) && (
                          <button
                            aria-label={`Receive ${order.orderNumber}`}
                            disabled={busyOrderId === order.id}
                            onClick={() => handleReceive(order)}
                            type="button"
                          >
                            Receive
                          </button>
                        )}

                        {canCancelPurchaseOrder(order.status) && (
                          <button
                            aria-label={`Cancel ${order.orderNumber}`}
                            className="button-danger"
                            disabled={busyOrderId === order.id}
                            onClick={() => handleCancel(order)}
                            type="button"
                          >
                            Cancel
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <nav className="pagination-bar" aria-label="Purchase order pagination">
            <button
              className="button-secondary"
              disabled={query.page <= 1}
              onClick={() => updateQuery('page', query.page - 1)}
              type="button"
            >
              Previous
            </button>
            <span>
              Page {data?.page ?? query.page} of {totalPages || 1}
              {' '}({data?.totalItems ?? 0} purchase orders)
            </span>
            <button
              className="button-secondary"
              disabled={query.page >= totalPages}
              onClick={() => updateQuery('page', query.page + 1)}
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

export default PurchaseOrdersPage;
