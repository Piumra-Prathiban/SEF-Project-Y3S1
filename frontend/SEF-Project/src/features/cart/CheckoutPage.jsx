import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Elements } from '@stripe/react-stripe-js';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { useAuth } from '../../contexts/AuthContext';
import { useSessionGuard } from '../../hooks/useSessionGuard';
import { getAddresses, getProfile } from '../profile/profileService';
import { createOrder, PaymentMethod } from '../../services/orderService';
import { formatCurrency } from '../../utils/format';
import { useCart } from './CartContext';
import { stripePromise } from './stripe';
import { StripePaymentForm } from './StripePaymentForm';
import '../storefront/storefront.css';
import './cart.css';

const PAYMENT_METHOD_LABELS = {
  [PaymentMethod.Card]: 'Card',
  [PaymentMethod.OnlineTransfer]: 'Online transfer',
  [PaymentMethod.Cash]: 'Cash on delivery',
};

export function CheckoutPage() {
  const { token } = useAuth();
  const guardSessionExpiry = useSessionGuard();
  const { items, estimatedSubtotal, clearCart } = useCart();

  const [address, setAddress] = useState({
    fullName: '',
    line1: '',
    line2: '',
    city: '',
    province: '',
    postalCode: '',
    country: 'Sri Lanka',
    phone: '',
  });
  const [paymentMethod, setPaymentMethod] = useState(String(PaymentMethod.Card));
  const [couponCode, setCouponCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [placedOrder, setPlacedOrder] = useState(null);
  const [paymentCompleted, setPaymentCompleted] = useState(false);
  const [pendingPayment, setPendingPayment] = useState(null);
  const [savedDetailsWarning, setSavedDetailsWarning] = useState(null);

  useEffect(() => {
    let active = true;

    Promise.all([getProfile(token), getAddresses(token)])
      .then(([profile, addresses]) => {
        if (!active) return;

        const defaultAddress = addresses.find((item) => item.isDefault) ?? addresses[0];
        setAddress((current) => ({
          ...current,
          fullName: current.fullName || [profile.firstName, profile.lastName].filter(Boolean).join(' '),
          line1: current.line1 || defaultAddress?.addressLine1 || '',
          line2: current.line2 || defaultAddress?.addressLine2 || '',
          city: current.city || defaultAddress?.city || '',
          province: current.province || defaultAddress?.province || '',
          postalCode: current.postalCode || defaultAddress?.postalCode || '',
          country: defaultAddress?.country || current.country || 'Sri Lanka',
        }));
      })
      .catch((requestError) => {
        if (active && !guardSessionExpiry(requestError)) {
          setSavedDetailsWarning(
            'Saved profile details could not be loaded. You can still enter the delivery address below.',
          );
        }
      });

    return () => { active = false; };
  }, [token, guardSessionExpiry]);

  function updateAddress(field, value) {
    setAddress((current) => ({ ...current, [field]: value }));
  }

  async function handleSubmit(event) {
    event.preventDefault();

    setIsSubmitting(true);
    setError(null);

    try {
      const order = await createOrder(token, {
        items: items.map((item) => ({
          productVariantId: item.variantId,
          quantity: item.quantity,
        })),
        deliveryAddress: {
          fullName: address.fullName,
          line1: address.line1,
          line2: address.line2 || null,
          city: address.city,
          province: address.province || null,
          postalCode: address.postalCode,
          country: address.country || null,
          phone: address.phone || null,
        },
        paymentMethod: Number(paymentMethod),
        couponCode: couponCode.trim() || null,
      });

      clearCart();

      const cardPayment = order.payments?.find((payment) => payment.clientSecret);

      if (stripePromise && cardPayment?.clientSecret) {
        setPendingPayment({ order, clientSecret: cardPayment.clientSecret });
      } else {
        setPlacedOrder(order);
      }
    } catch (err) {
      setError(err?.message || 'The order could not be placed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  if (pendingPayment) {
    return (
      <div className="storefront">
        <div className="storefront__catalogue">
          <h1>Complete your payment</h1>
          <p className="storefront__lede">
            Order <strong>{pendingPayment.order.orderNumber}</strong> is ready.
            Pay now with your card to confirm it.
          </p>
          <div className="checkout">
            <Elements
              stripe={stripePromise}
              options={{ clientSecret: pendingPayment.clientSecret }}
            >
              <StripePaymentForm
                order={pendingPayment.order}
                onSuccess={() => {
                  setPlacedOrder(pendingPayment.order);
                  setPaymentCompleted(true);
                  setPendingPayment(null);
                }}
              />
            </Elements>
          </div>
        </div>
      </div>
    );
  }

  if (placedOrder) {
    return (
      <div className="storefront">
        <div className="storefront__catalogue">
          <h1>Order placed</h1>
          <p className="storefront__lede">
            {paymentCompleted
              ? (
                <>
                  Your order <strong>{placedOrder.orderNumber}</strong> was
                  placed and your card payment succeeded.
                </>
              )
              : (
                <>
                  Your order <strong>{placedOrder.orderNumber}</strong> was
                  created. Your payment is recorded as pending until staff
                  confirm it.
                </>
              )}
          </p>

          {placedOrder.discountTotal > 0 && (
            <Alert>
              You saved {formatCurrency(placedOrder.discountTotal, placedOrder.currency)}.
            </Alert>
          )}
          <p>
            <Link className="storefront__cta" to={`/orders/${placedOrder.id}`}>
              View your order
            </Link>
          </p>
        </div>
      </div>
    );
  }

  if (items.length === 0) {
    return (
      <div className="storefront">
        <div className="storefront__catalogue">
          <h1>Checkout</h1>
          <div className="empty-state">
            <p>Your cart is empty, so there is nothing to check out.</p>
            <Link className="storefront__cta" to="/">
              Shop the collection
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="storefront">
      <div className="storefront__catalogue">
        <h1>Checkout</h1>

        <div className="checkout">
          <form className="checkout__form" onSubmit={handleSubmit}>
            <h2>Delivery address</h2>

            {savedDetailsWarning && <Alert>{savedDetailsWarning}</Alert>}

            <div className="checkout__grid">
              <label>
                Full name
                <input
                  onChange={(event) => updateAddress('fullName', event.target.value)}
                  required
                  value={address.fullName}
                />
              </label>
              <label>
                Address line 1
                <input
                  onChange={(event) => updateAddress('line1', event.target.value)}
                  required
                  value={address.line1}
                />
              </label>
              <label>
                Address line 2
                <input
                  onChange={(event) => updateAddress('line2', event.target.value)}
                  value={address.line2}
                />
              </label>
              <label>
                City
                <input
                  onChange={(event) => updateAddress('city', event.target.value)}
                  required
                  value={address.city}
                />
              </label>
              <label>
                Province
                <input
                  onChange={(event) => updateAddress('province', event.target.value)}
                  value={address.province}
                />
              </label>
              <label>
                Postal code
                <input
                  onChange={(event) => updateAddress('postalCode', event.target.value)}
                  required
                  value={address.postalCode}
                />
              </label>
              <label>
                Country
                <input
                  onChange={(event) => updateAddress('country', event.target.value)}
                  value={address.country}
                />
              </label>
              <label>
                Phone
                <input
                  onChange={(event) => updateAddress('phone', event.target.value)}
                  value={address.phone}
                />
              </label>
            </div>

            <h2>Payment</h2>
            <label>
              Coupon code <span className="checkout__optional">Optional</span>
              <input
                autoCapitalize="characters"
                onChange={(event) => setCouponCode(event.target.value.toUpperCase())}
                placeholder="Enter a code"
                value={couponCode}
              />
            </label>
            <label>
              Payment method
              <select
                onChange={(event) => setPaymentMethod(event.target.value)}
                value={paymentMethod}
              >
                {Object.entries(PAYMENT_METHOD_LABELS).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <p className="cart-summary__note">
              {stripePromise
                ? 'Card payments are collected securely by Stripe (test mode). Other methods are recorded as pending for staff confirmation.'
                : 'No card details are collected here: your chosen method is recorded as a pending payment, and staff confirm it through the dashboard.'}
            </p>

            <ApiErrorAlert message={error} />

            <button disabled={isSubmitting} type="submit">
              {isSubmitting ? 'Placing order...' : 'Place order'}
            </button>
          </form>

          <aside className="checkout__summary">
            <h2>Order summary</h2>
            <ul className="checkout__items">
              {items.map((item) => (
                <li key={item.variantId}>
                  <span>
                    {item.productName}
                    <br />
                    <small>
                      {item.sizeName} · {item.colourName} · x{item.quantity}
                    </small>
                  </span>
                  <span>{formatCurrency(item.price * item.quantity, 'LKR')}</span>
                </li>
              ))}
            </ul>
            <p>
              Estimated subtotal:{' '}
              <strong>{formatCurrency(estimatedSubtotal, 'LKR')}</strong>
            </p>
            <Alert>
              Final totals, stock reservation and taxes are applied by the
              server when the order is placed.
            </Alert>
          </aside>
        </div>
      </div>
    </div>
  );
}

export default CheckoutPage;
