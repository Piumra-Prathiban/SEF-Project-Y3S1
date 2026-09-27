import { useEffect, useMemo, useState } from 'react';
import {
  cancelReturn,
  createReturn,
  getOrderReturns,
  OrderStatus,
  ReturnReason,
  ReturnReasonName,
  ReturnStatus,
  ReturnStatusName,
  updateReturnStatus,
} from '../../services/orderService';
import { formatCurrency, formatDateTime } from '../../utils/format';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import StatusBadge from '../../components/StatusBadge';

const HOLDING_STATUSES = new Set([
  ReturnStatus.Requested,
  ReturnStatus.Approved,
  ReturnStatus.Received,
  ReturnStatus.Refunded,
]);

const STAFF_TRANSITIONS = {
  [ReturnStatus.Requested]: [ReturnStatus.Approved, ReturnStatus.Rejected],
  [ReturnStatus.Approved]: [ReturnStatus.Received],
  [ReturnStatus.Received]: [ReturnStatus.Refunded],
  [ReturnStatus.Rejected]: [],
  [ReturnStatus.Refunded]: [],
  [ReturnStatus.Cancelled]: [],
};

function ReturnsSection({ order, token, canManage, onOrderRefresh }) {
  const guardSessionExpiry = useSessionGuard();
  const [returns, setReturns] = useState([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [reason, setReason] = useState(String(ReturnReason.WrongSize));
  const [note, setNote] = useState('');
  const [quantities, setQuantities] = useState({});

  async function loadReturns() {
    setLoading(true);
    setError('');
    try {
      setReturns(await getOrderReturns(token, order.id));
    } catch (requestError) {
      if (!guardSessionExpiry(requestError)) {
        setError(requestError.message || 'Returns could not be loaded.');
      }
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    // The order identifier is the boundary for this independently loaded data.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadReturns();
    // Reloading is keyed to the order. Mutations refresh explicitly.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [order.id, token]);

  const remainingByItem = useMemo(() => {
    const held = {};
    returns
      .filter((itemReturn) => HOLDING_STATUSES.has(itemReturn.status))
      .flatMap((itemReturn) => itemReturn.items)
      .forEach((item) => {
        held[item.orderItemId] = (held[item.orderItemId] || 0) + item.quantity;
      });

    return Object.fromEntries(
      order.items.map((item) => [
        item.id,
        Math.max(0, item.quantity - (held[item.id] || 0)),
      ]),
    );
  }, [order.items, returns]);

  const hasReturnableItems = Object.values(remainingByItem).some(
    (quantity) => quantity > 0,
  );

  async function submitReturn(event) {
    event.preventDefault();
    const items = order.items
      .map((item) => ({
        orderItemId: item.id,
        quantity: Number(quantities[item.id] || 0),
      }))
      .filter((item) => item.quantity > 0);

    if (items.length === 0) {
      setError('Choose at least one item and quantity to return.');
      return;
    }

    setBusy(true);
    setError('');
    setSuccess('');
    try {
      await createReturn(token, order.id, {
        reason: Number(reason),
        note: note.trim() || undefined,
        items,
      });
      setQuantities({});
      setNote('');
      setSuccess('Return request submitted for staff review.');
      await loadReturns();
    } catch (requestError) {
      if (!guardSessionExpiry(requestError)) {
        setError(requestError.message || 'The return could not be requested.');
      }
    } finally {
      setBusy(false);
    }
  }

  async function changeStatus(itemReturn, status) {
    const needsConfirmation =
      status === ReturnStatus.Received || status === ReturnStatus.Refunded;
    if (
      needsConfirmation
      && !window.confirm(
        status === ReturnStatus.Received
          ? 'Confirm these items were received? This restores their stock.'
          : 'Issue this refund? This records a refund and cannot be undone.',
      )
    ) {
      return;
    }

    setBusy(true);
    setError('');
    setSuccess('');
    try {
      await updateReturnStatus(token, itemReturn.id, { status });
      setSuccess(`Return updated to ${ReturnStatusName[status]}.`);
      await loadReturns();
      if (status === ReturnStatus.Refunded) await onOrderRefresh();
    } catch (requestError) {
      if (!guardSessionExpiry(requestError)) {
        setError(requestError.message || 'The return could not be updated.');
      }
    } finally {
      setBusy(false);
    }
  }

  async function handleCancel(itemReturn) {
    if (!window.confirm('Cancel this return request?')) return;
    setBusy(true);
    setError('');
    setSuccess('');
    try {
      await cancelReturn(token, itemReturn.id);
      setSuccess('Return request cancelled.');
      await loadReturns();
    } catch (requestError) {
      if (!guardSessionExpiry(requestError)) {
        setError(requestError.message || 'The return could not be cancelled.');
      }
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="order-detail__returns" aria-label="Returns">
      <div className="returns-heading">
        <div>
          <p className="section-eyebrow">After delivery</p>
          <h2>Returns & refunds</h2>
        </div>
        <span className="returns-count">{returns.length} request{returns.length === 1 ? '' : 's'}</span>
      </div>

      {error && <p className="status-feedback status-feedback--error" role="alert">{error}</p>}
      {success && <p className="status-feedback status-feedback--success" role="status">{success}</p>}
      {loading ? (
        <p className="order-detail__empty" aria-busy="true">Loading returns…</p>
      ) : returns.length === 0 ? (
        <p className="order-detail__empty">No returns have been requested for this order.</p>
      ) : (
        <div className="returns-list">
          {returns.map((itemReturn) => (
            <article className="return-card" key={itemReturn.id}>
              <div className="return-card__header">
                <div>
                  <strong>{itemReturn.returnNumber}</strong>
                  <p>{ReturnReasonName[itemReturn.reason]} · requested <time dateTime={itemReturn.requestedAt}>{formatDateTime(itemReturn.requestedAt)}</time></p>
                </div>
                <StatusBadge status={itemReturn.status} kind="return" />
              </div>
              <ul className="return-card__items">
                {itemReturn.items.map((item) => (
                  <li key={item.id}>{item.name} ({item.sku}) × {item.quantity}</li>
                ))}
              </ul>
              <p className="return-card__refund">Expected refund <strong>{formatCurrency(itemReturn.refundAmount, itemReturn.currency)}</strong></p>
              {itemReturn.customerNote && <p>Customer: {itemReturn.customerNote}</p>}
              {itemReturn.staffNote && <p>Staff: {itemReturn.staffNote}</p>}

              {canManage && (STAFF_TRANSITIONS[itemReturn.status] || []).length > 0 && (
                <div className="return-actions" aria-label={`Manage ${itemReturn.returnNumber}`}>
                  {(STAFF_TRANSITIONS[itemReturn.status] || []).map((status) => (
                    <button key={status} type="button" disabled={busy} onClick={() => changeStatus(itemReturn, status)}>
                      {status === ReturnStatus.Received ? 'Confirm received' : status === ReturnStatus.Refunded ? 'Issue refund' : ReturnStatusName[status]}
                    </button>
                  ))}
                </div>
              )}
              {!canManage && itemReturn.status === ReturnStatus.Requested && (
                <button className="button-secondary" type="button" disabled={busy} onClick={() => handleCancel(itemReturn)}>Cancel request</button>
              )}
            </article>
          ))}
        </div>
      )}

      {!canManage && order.status === OrderStatus.Completed && hasReturnableItems && (
        <form className="return-request-form" onSubmit={submitReturn}>
          <h3>Request a return</h3>
          <p>Select only the items you are sending back. Staff must approve and receive them before a refund is issued.</p>
          <div className="return-item-picker">
            {order.items.map((item) => {
              const remaining = remainingByItem[item.id] || 0;
              return (
                <label key={item.id}>
                  <span>{item.name} <small>{item.sku} · {remaining} returnable</small></span>
                  <input type="number" min="0" max={remaining} value={quantities[item.id] || ''} disabled={remaining === 0 || busy} onChange={(event) => setQuantities((current) => ({ ...current, [item.id]: event.target.value }))} />
                </label>
              );
            })}
          </div>
          <label>
            Reason
            <select value={reason} onChange={(event) => setReason(event.target.value)}>
              {Object.entries(ReturnReason).map(([name, value]) => <option key={name} value={value}>{ReturnReasonName[value]}</option>)}
            </select>
          </label>
          <label>
            Details <span>(optional)</span>
            <textarea maxLength="1000" value={note} onChange={(event) => setNote(event.target.value)} placeholder="Tell us what went wrong or what condition the item is in." />
          </label>
          <button type="submit" disabled={busy}>{busy ? 'Submitting…' : 'Submit return request'}</button>
        </form>
      )}
    </section>
  );
}

export default ReturnsSection;
