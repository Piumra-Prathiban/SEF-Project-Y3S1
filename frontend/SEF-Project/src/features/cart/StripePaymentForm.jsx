import { useState } from 'react';
import { PaymentElement, useStripe, useElements } from '@stripe/react-stripe-js';
import { Alert } from '../../components/ui/Alert';
import { formatCurrency } from '../../utils/format';

/**
 * Confirms a Stripe PaymentIntent created during checkout. Rendered inside an
 * <Elements> provider, so it has access to the client secret via the provider's
 * options. On success it notifies the parent; the backend webhook is the
 * authority that flips the payment to Completed.
 */
export function StripePaymentForm({ order, onSuccess }) {
  const stripe = useStripe();
  const elements = useElements();
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState(null);

  async function handlePay(event) {
    event.preventDefault();

    if (!stripe || !elements) {
      return;
    }

    setProcessing(true);
    setError(null);

    const { error: confirmError } = await stripe.confirmPayment({
      elements,
      redirect: 'if_required',
      confirmParams: {
        return_url: `${window.location.origin}/orders/${order.id}`,
      },
    });

    if (confirmError) {
      setError(confirmError.message || 'The payment could not be completed.');
      setProcessing(false);
      return;
    }

    onSuccess();
  }

  return (
    <form className="checkout__form" onSubmit={handlePay}>
      <PaymentElement />
      <button disabled={!stripe || processing} type="submit">
        {processing
          ? 'Processing payment…'
          : `Pay ${formatCurrency(order.total, order.currency)}`}
      </button>
      {error && <Alert>{error}</Alert>}
    </form>
  );
}

export default StripePaymentForm;
