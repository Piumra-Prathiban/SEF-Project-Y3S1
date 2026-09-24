import { useState } from 'react';
import { cancelOrder, OrderStatus } from '../../services/orderService';
import './orders.css';

// The backend decides whether an order can actually be cancelled (order status,
// shipped shipments) - this component deliberately does not encode those rules.
// It only hides the action for states where cancellation can never apply again,
// and surfaces the server's explanation for anything it rejects.
const TERMINAL_STATUSES = [OrderStatus.Cancelled, OrderStatus.Refunded];

function describeCancelError(error) {
  if (error?.status === 403) {
    return 'You do not have permission to cancel this order.';
  }

  if (error?.status === 404) {
    return 'This order could not be found.';
  }

  return error?.message || 'The order could not be cancelled.';
}

function CancelOrderSection({ order, token, onCancelled }) {
  const [cancelling, setCancelling] = useState(false);
  const [error, setError] = useState(null);
  const [success, setSuccess] = useState(null);

  const canCancel = !TERMINAL_STATUSES.includes(order.status);

  if (!canCancel) {
    return (
      <section className="order-detail__cancel" aria-label="Cancel order">
        <h2>Cancel order</h2>
        <p className="order-detail__empty">
          This order can no longer be cancelled.
        </p>

        {success && (
          <p className="status-feedback status-feedback--success" role="status">
            {success}
          </p>
        )}
      </section>
    );
  }

  async function handleCancel() {
    const confirmed = window.confirm(
      'Cancel this order? This cannot be undone. Reserved stock is released, '
        + 'unshipped shipments are cancelled and completed payments are refunded.',
    );

    if (!confirmed) {
      return;
    }

    setCancelling(true);
    setError(null);

    try {
      const updatedOrder = await cancelOrder(token, order.id);

      setSuccess('Order cancelled.');
      onCancelled(updatedOrder);
    } catch (err) {
      setError(describeCancelError(err));
    } finally {
      setCancelling(false);
    }
  }

  return (
    <section className="order-detail__cancel" aria-label="Cancel order">
      <h2>Cancel order</h2>

      <p className="order-detail__empty">
        Cancellation cannot be undone. Reserved stock is released, unshipped
        shipments are cancelled and completed payments are refunded.
      </p>

      <button
        type="button"
        className="cancel-order__button"
        onClick={handleCancel}
        disabled={cancelling}
      >
        {cancelling ? 'Cancelling…' : 'Cancel order'}
      </button>

      {error && (
        <p className="status-feedback status-feedback--error" role="alert">
          {error}
        </p>
      )}
    </section>
  );
}

export default CancelOrderSection;
