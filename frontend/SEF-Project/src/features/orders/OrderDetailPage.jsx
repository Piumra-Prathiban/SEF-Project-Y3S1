import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { getOrderById } from '../../services/orderService';
import { formatCurrency, formatDateTime } from '../../utils/format';
import Loading from '../../components/Loading';
import ErrorAlert from '../../components/ErrorAlert';
import StatusBadge from '../../components/StatusBadge';
import './orders.css';

function OrderDetailPage() {
  const { id } = useParams();
  const { token } = useAuth();

  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [retryTick, setRetryTick] = useState(0);

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

  if (loading) {
    return (
      <div className="orders-page">
        <Loading />
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

      <section className="order-detail__items">
        <h2>Items</h2>
        <table className="orders-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>SKU</th>
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
            {order.deliveryAddress.fullName}
            <br />
            {order.deliveryAddress.line1}
            {order.deliveryAddress.line2 && `, ${order.deliveryAddress.line2}`}
            <br />
            {order.deliveryAddress.city}
            {order.deliveryAddress.province &&
              `, ${order.deliveryAddress.province}`}
            <br />
            {order.deliveryAddress.postalCode} {order.deliveryAddress.country}
          </address>
        </section>
      )}
    </div>
  );
}

export default OrderDetailPage;
