import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { useAuth } from '../../contexts/AuthContext';
import { createOrder, createPayment, PaymentMethod } from '../../services/orderService';
import { formatCurrency } from '../../utils/format';
import { useCart } from './CartContext';
import '../storefront/storefront.css';
import './cart.css';

const PAYMENT_METHOD_LABELS = {
  [PaymentMethod.Card]: 'Card',
  [PaymentMethod.OnlineTransfer]: 'Online transfer',
  [PaymentMethod.Cash]: 'Cash on delivery',
};

export function CheckoutPage() {
  const { token } = useAuth();
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
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState(null);
  const [placedOrder, setPlacedOrder] = useState(null);

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
      });

      // The order exists from here on, so a payment failure must not lose it:
      // record the chosen method as a pending payment and warn instead.
      let paymentWarning = null;

      try {
        await createPayment(token, order.id, {
          method: Number(paymentMethod),
          amount: order.total,
        });
      } catch {
        paymentWarning = 'Your order is placed, but the payment could not be recorded. Record it from the order page.';
      }

      clearCart();
      setPlacedOrder({ ...order, paymentWarning });
    } catch (err) {
      setError(err?.message || 'The order could not be placed.');
    } finally {
      setIsSubmitting(false);
    }
  }

  if (placedOrder) {
    return (
      <div className="storefront">
        <div className="storefront__catalogue">
          <h1>Order placed</h1>
          <p className="storefront__lede">
            Your order <strong>{placedOrder.orderNumber}</strong> was created.
            Your payment is recorded as pending until staff confirm it.
          </p>

          {placedOrder.paymentWarning && (
            <Alert tone="danger">{placedOrder.paymentWarning}</Alert>
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
              No card details are collected here: your chosen method is recorded
              as a pending payment, and staff confirm it through the dashboard.
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
