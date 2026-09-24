import { useState } from 'react';
import {
  updatePaymentStatus,
  PaymentMethodName,
  PaymentStatus,
  PaymentStatusName,
} from '../../services/orderService';
import { describePaymentError } from './paymentErrors';
import './orders.css';

// Advisory only: mirrors the backend's payment status state machine so staff get
// sensible options. The backend enforces the real transitions (409 on rejection)
// and stays authoritative for the payment status itself.
const ALLOWED_PAYMENT_TRANSITIONS = {
  [PaymentStatus.Pending]: [PaymentStatus.Completed, PaymentStatus.Failed],
  [PaymentStatus.Completed]: [PaymentStatus.Refunded],
  [PaymentStatus.Failed]: [],
  [PaymentStatus.Refunded]: [],
};

const CONFIRM_STATUSES = [PaymentStatus.Completed, PaymentStatus.Refunded];

function PaymentStatusForm({ token, orderId, payment, onUpdated }) {
  const [selectedStatus, setSelectedStatus] = useState('');
  const [updating, setUpdating] = useState(false);
  const [error, setError] = useState(null);

  const allowedStatusOptions = ALLOWED_PAYMENT_TRANSITIONS[payment.status] ?? [];

  if (allowedStatusOptions.length === 0) {
    return <span className="payment-actions__none">—</span>;
  }

  async function handleSubmit(event) {
    event.preventDefault();

    const targetStatus = Number(selectedStatus);

    if (!allowedStatusOptions.includes(targetStatus)) {
      setError('Select one of the available payment statuses.');
      return;
    }

    if (
      CONFIRM_STATUSES.includes(targetStatus)
      && !window.confirm(
        `Mark this payment as ${PaymentStatusName[targetStatus]}?`,
      )
    ) {
      return;
    }

    setUpdating(true);
    setError(null);

    try {
      await updatePaymentStatus(token, orderId, payment.id, {
        status: targetStatus,
      });

      setSelectedStatus('');
      onUpdated(`Payment marked as ${PaymentStatusName[targetStatus]}.`);
    } catch (err) {
      setError(describePaymentError(err));
    } finally {
      setUpdating(false);
    }
  }

  return (
    <form className="payment-actions" onSubmit={handleSubmit}>
      <select
        aria-label={`Update ${PaymentMethodName[payment.method] ?? 'payment'} payment`}
        value={selectedStatus}
        onChange={(event) => setSelectedStatus(event.target.value)}
      >
        <option value="">Select…</option>
        {allowedStatusOptions.map((statusOption) => (
          <option key={statusOption} value={statusOption}>
            {PaymentStatusName[statusOption]}
          </option>
        ))}
      </select>

      <button type="submit" disabled={updating || selectedStatus === ''}>
        {updating ? 'Saving…' : 'Apply'}
      </button>

      {error && (
        <p className="status-feedback status-feedback--error" role="alert">
          {error}
        </p>
      )}
    </form>
  );
}

export default PaymentStatusForm;
