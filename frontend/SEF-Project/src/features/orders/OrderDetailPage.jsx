import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import {
  getOrderById,
  updateOrderStatus,
  createPayment,
  OrderStatus,
  OrderStatusName,
  PaymentMethod,
  PaymentMethodName,
} from '../../services/orderService';
import { formatCurrency, formatDateTime } from '../../utils/format';
import { isStaff } from '../../utils/roles';
import { describePaymentError } from './paymentErrors';
import Loading from '../../components/Loading';
import ErrorAlert from '../../components/ErrorAlert';
import StatusBadge from '../../components/StatusBadge';
import PaymentStatusForm from './PaymentStatusForm';
import './orders.css';

// Advisory only: mirrors the backend's order status state machine so the UI can
// offer sensible next steps. The backend enforces the real transitions and a
// rejected change surfaces as a 409. Cancellation is deliberately excluded -
// it is owned by the dedicated POST /orders/{id}/cancel flow, which also
// releases reserved inventory and refunds payments.
const ALLOWED_STATUS_TRANSITIONS = {
  [OrderStatus.Pending]: [OrderStatus.Confirmed],
  [OrderStatus.Confirmed]: [OrderStatus.Preparing],
  [OrderStatus.Preparing]: [OrderStatus.Ready],
  [OrderStatus.Ready]: [OrderStatus.Completed],
  [OrderStatus.Completed]: [OrderStatus.Refunded],
  [OrderStatus.Cancelled]: [],
  [OrderStatus.Refunded]: [],
};

const TERMINAL_STATUSES = [OrderStatus.Completed, OrderStatus.Refunded];

function describeStatusError(error) {
  if (error?.status === 403) {
    return 'You do not have permission to change the order status.';
  }

  if (error?.status === 409) {
    return 'That status change is not allowed from the current status.';
  }

  if (error?.status === 400) {
    return error.message || 'The status change was rejected.';
  }

  return error?.message || 'The status change could not be applied.';
}

function OrderDetailPage() {
  const { id } = useParams();
  const { token, user } = useAuth();

  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [retryTick, setRetryTick] = useState(0);
  const [selectedStatus, setSelectedStatus] = useState('');
  const [updating, setUpdating] = useState(false);
  const [statusError, setStatusError] = useState(null);
  const [statusSuccess, setStatusSuccess] = useState(null);
  const [paymentMethod, setPaymentMethod] = useState('');
  const [paymentAmount, setPaymentAmount] = useState('');
  const [creatingPayment, setCreatingPayment] = useState(false);
  const [paymentError, setPaymentError] = useState(null);
  const [paymentSuccess, setPaymentSuccess] = useState(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const response = await getOrderById(token, id);

        if (!cancelled) {
          setOrder(response);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    load();

    return () => {
      cancelled = true;
    };
  }, [token, id, retryTick]);

  const canManageStatus = isStaff(user);
  const allowedStatusOptions = order
    ? (ALLOWED_STATUS_TRANSITIONS[order.status] ?? [])
    : [];

  async function refreshOrder() {
    const response = await getOrderById(token, id);
    setOrder(response);
  }

  async function handleStatusSubmit(event) {
    event.preventDefault();

    const targetStatus = Number(selectedStatus);

    if (!allowedStatusOptions.includes(targetStatus)) {
      setStatusError({ message: 'Select one of the available statuses.' });
      return;
    }

    if (
      TERMINAL_STATUSES.includes(targetStatus)
      && !window.confirm(
        `Change the order status to ${OrderStatusName[targetStatus]}? This cannot be undone.`,
      )
    ) {
      return;
    }

    setUpdating(true);
    setStatusError(null);
    setStatusSuccess(null);

    try {
      const updated = await updateOrderStatus(token, id, {
        status: targetStatus,
      });

      setOrder(updated);
      setSelectedStatus('');
      setStatusSuccess(
        `Order status updated to ${OrderStatusName[targetStatus]}.`,
      );
    } catch (err) {
      setStatusError(err);
    } finally {
      setUpdating(false);
    }
  }

  async function handlePaymentUpdated(message) {
    setPaymentSuccess(message);
    setPaymentError(null);

    try {
      await refreshOrder();
    } catch (err) {
      setPaymentError(err);
    }
  }

  async function handlePaymentSubmit(event) {
    event.preventDefault();

    setCreatingPayment(true);
    setPaymentError(null);
    setPaymentSuccess(null);

    try {
      await createPayment(token, id, {
        method: Number(paymentMethod),
        amount: Number(paymentAmount),
      });

      await refreshOrder();
      setPaymentMethod('');
      setPaymentAmount('');
      setPaymentSuccess(
        'Payment recorded with status Pending. Staff confirm it once processed.',
      );
    } catch (err) {
      setPaymentError(err);
    } finally {
      setCreatingPayment(false);
    }
  }

  if (loading) {
    return (
      <div className="orders-page">
        <Loading />
      </div>
    );
  }

  if (error?.status === 404) {
    return (
      <div className="orders-page">
        <h1>Order not found</h1>
        <p className="order-detail__empty">
          This order does not exist or you do not have access to it.
        </p>
        <Link to="/orders" className="order-detail__back">
          Back to orders
        </Link>
      </div>
    );
  }

  if (error) {
    return (
      <div className="orders-page">
        <ErrorAlert
          error={error}
          onRetry={() => setRetryTick((tick) => tick + 1)}
        />
      </div>
    );
  }

  if (!order) {
    return null;
  }

  return (
    <div className="orders-page">
      <Link to="/orders" className="order-detail__back">
        Back to orders
      </Link>

      <h1>Order {order.orderNumber}</h1>

      <div className="order-detail__meta">
        <StatusBadge status={order.status} />
        <span>Placed {formatDateTime(order.placedAt)}</span>
      </div>

      {canManageStatus && (
        <section className="order-detail__status-management">
          <h2>Update status</h2>

          {allowedStatusOptions.length === 0 ? (
            <p className="order-detail__empty">
              No further status changes are available for this order.
            </p>
          ) : (
            <form className="status-form" onSubmit={handleStatusSubmit}>
              <label htmlFor="order-status-select">
                New status
                <select
                  id="order-status-select"
                  value={selectedStatus}
                  onChange={(event) => setSelectedStatus(event.target.value)}
                >
                  <option value="">Select a status…</option>
                  {allowedStatusOptions.map((statusOption) => (
                    <option key={statusOption} value={statusOption}>
                      {OrderStatusName[statusOption]}
                    </option>
                  ))}
                </select>
              </label>

              <button type="submit" disabled={updating || selectedStatus === ''}>
                {updating ? 'Updating…' : 'Update status'}
              </button>
            </form>
          )}

          {statusSuccess && (
            <p className="status-feedback status-feedback--success" role="status">
              {statusSuccess}
            </p>
          )}

          {statusError && (
            <p className="status-feedback status-feedback--error" role="alert">
              {describeStatusError(statusError)}
            </p>
          )}
        </section>
      )}

      <section className="order-detail__items">
        <h2>Items</h2>
        <table className="orders-table">
          <thead>
            <tr>
              <th>Product</th>
              <th>Variant (SKU)</th>
              <th>Quantity</th>
              <th>Unit price</th>
              <th>Line total</th>
            </tr>
          </thead>
          <tbody>
            {order.items.map((item) => (
              <tr key={item.id}>
                <td>{item.name}</td>
                <td>{item.sku}</td>
                <td>{item.quantity}</td>
                <td>{formatCurrency(item.unitPrice, order.currency)}</td>
                <td>{formatCurrency(item.lineTotal, order.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="order-detail__totals">
        <h2>Totals</h2>
        <dl>
          <div>
            <dt>Subtotal</dt>
            <dd>{formatCurrency(order.subtotal, order.currency)}</dd>
          </div>
          <div>
            <dt>Discount</dt>
            <dd>{formatCurrency(order.discountTotal, order.currency)}</dd>
          </div>
          <div>
            <dt>Tax</dt>
            <dd>{formatCurrency(order.taxAmount, order.currency)}</dd>
          </div>
          <div>
            <dt>Shipping</dt>
            <dd>{formatCurrency(order.shippingFee, order.currency)}</dd>
          </div>
          <div className="order-detail__total-row">
            <dt>Total</dt>
            <dd>{formatCurrency(order.total, order.currency)}</dd>
          </div>
        </dl>
      </section>

      {order.deliveryAddress && (
        <section className="order-detail__address">
          <h2>Delivery address</h2>
          <address>
            <span>{order.deliveryAddress.fullName}</span>
            <span>
              {order.deliveryAddress.line1}
              {order.deliveryAddress.line2 &&
                `, ${order.deliveryAddress.line2}`}
            </span>
            <span>
              {order.deliveryAddress.city}
              {order.deliveryAddress.province &&
                `, ${order.deliveryAddress.province}`}
            </span>
            <span>
              {order.deliveryAddress.postalCode} {order.deliveryAddress.country}
            </span>
            {order.deliveryAddress.phone && (
              <span>{order.deliveryAddress.phone}</span>
            )}
          </address>
        </section>
      )}

      <section className="order-detail__payments" aria-label="Payments">
        <h2>Payments</h2>

        {order.payments.length === 0 ? (
          <p className="order-detail__empty">No payments recorded.</p>
        ) : (
          <table className="orders-table">
            <thead>
              <tr>
                <th>Method</th>
                <th>Status</th>
                <th>Amount</th>
                <th>Paid at</th>
                <th>Reference</th>
                {canManageStatus && <th>Update</th>}
              </tr>
            </thead>
            <tbody>
              {order.payments.map((payment) => (
                <tr key={payment.id}>
                  <td>{PaymentMethodName[payment.method] ?? payment.method}</td>
                  <td>
                    <StatusBadge status={payment.status} kind="payment" />
                  </td>
                  <td>{formatCurrency(payment.amount, order.currency)}</td>
                  <td>{payment.paidAt ? formatDateTime(payment.paidAt) : '—'}</td>
                  <td>{payment.transactionReference ?? '—'}</td>
                  {canManageStatus && (
                    <td>
                      <PaymentStatusForm
                        token={token}
                        orderId={order.id}
                        payment={payment}
                        onUpdated={handlePaymentUpdated}
                      />
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {paymentSuccess && (
          <p className="status-feedback status-feedback--success" role="status">
            {paymentSuccess}
          </p>
        )}

        {paymentError && (
          <p className="status-feedback status-feedback--error" role="alert">
            {describePaymentError(paymentError)}
          </p>
        )}

        <form className="payment-form" onSubmit={handlePaymentSubmit}>
          <h3>Record a payment</h3>

          <label htmlFor="payment-method">
            Method
            <select
              id="payment-method"
              value={paymentMethod}
              onChange={(event) => setPaymentMethod(event.target.value)}
            >
              <option value="">Select a method…</option>
              {Object.entries(PaymentMethod).map(([name, value]) => (
                <option key={name} value={value}>
                  {name}
                </option>
              ))}
            </select>
          </label>

          <label htmlFor="payment-amount">
            Amount ({order.currency})
            <input
              id="payment-amount"
              type="number"
              min="0.01"
              step="0.01"
              value={paymentAmount}
              onChange={(event) => setPaymentAmount(event.target.value)}
            />
          </label>

          <button
            type="submit"
            disabled={
              creatingPayment || paymentMethod === '' || paymentAmount === ''
            }
          >
            {creatingPayment ? 'Submitting…' : 'Submit payment'}
          </button>

          <p className="payment-form__note">
            Order total {formatCurrency(order.total, order.currency)}. Only the
            method and amount are recorded — no card details are collected or
            stored. Submitted payments start as Pending and the server checks the
            outstanding balance before accepting them.
          </p>
        </form>
      </section>

      <section className="order-detail__shipments">
        <h2>Shipments</h2>
        {order.shipments.length === 0 ? (
          <p className="order-detail__empty">No shipments recorded.</p>
        ) : (
          <table className="orders-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Carrier</th>
                <th>Tracking number</th>
                <th>Shipped at</th>
                <th>Delivered at</th>
              </tr>
            </thead>
            <tbody>
              {order.shipments.map((shipment) => (
                <tr key={shipment.id}>
                  <td>
                    <StatusBadge status={shipment.status} kind="shipment" />
                  </td>
                  <td>{shipment.carrier ?? '—'}</td>
                  <td>{shipment.trackingNumber ?? '—'}</td>
                  <td>
                    {shipment.shippedAt ? formatDateTime(shipment.shippedAt) : '—'}
                  </td>
                  <td>
                    {shipment.deliveredAt
                      ? formatDateTime(shipment.deliveredAt)
                      : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="order-detail__history">
        <h2>Status history</h2>
        {order.statusHistory.length === 0 ? (
          <p className="order-detail__empty">No status changes recorded.</p>
        ) : (
          <ol className="timeline" aria-label="Status history timeline">
            {order.statusHistory.map((entry) => (
              <li key={entry.id} className="timeline__item">
                <div className="timeline__header">
                  <StatusBadge status={entry.status} />
                  <span className="timeline__time">
                    {formatDateTime(entry.changedAt)}
                  </span>
                </div>
                {entry.note && <p className="timeline__note">{entry.note}</p>}
              </li>
            ))}
          </ol>
        )}
      </section>
    </div>
  );
}

export default OrderDetailPage;
