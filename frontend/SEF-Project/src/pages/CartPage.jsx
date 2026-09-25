import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState';
import { getApiErrorMessage } from '../services/api';
import { clearCart, getCart, removeCartItem, updateCartItem } from '../services/shoppingService';
import { formatCurrency } from '../utils/formatters';

function CartRow({ item, currency, busy, onUpdate, onRemove }) {
  const [quantity, setQuantity] = useState(item.quantity);

  return (
    <article className="cart-row">
      <div className="cart-row__visual" aria-hidden="true">{item.productName.slice(0, 1)}</div>
      <div className="cart-row__details">
        <p className="kicker">{item.sku}</p>
        <h2>{item.productName}</h2>
        <p>{item.variantName}</p>
        <p className={item.hasSufficientStock ? 'stock stock--yes' : 'stock stock--no'}>{item.hasSufficientStock ? `${item.availableQuantity} available` : 'Quantity no longer available'}</p>
      </div>
      <div className="cart-row__quantity">
        <label>Quantity<input aria-label={`Quantity for ${item.productName}`} type="number" min="1" max={item.availableQuantity || 1} value={quantity} onChange={(event) => setQuantity(Number(event.target.value))} /></label>
        <button type="button" className="text-button" disabled={busy || quantity < 1 || quantity === item.quantity} onClick={() => onUpdate(item.id, quantity)}>Update</button>
        <button type="button" className="text-button text-button--danger" disabled={busy} onClick={() => onRemove(item.id)}>Remove</button>
      </div>
      <div className="cart-row__price"><span>{formatCurrency(item.unitPrice, currency)} each</span><strong>{formatCurrency(item.lineTotal, currency)}</strong></div>
    </article>
  );
}

export default function CartPage() {
  const { token } = useAuth();
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const loadCart = useCallback(async () => {
    await Promise.resolve();
    setLoading(true);
    setError('');
    try { setCart(await getCart(token)); }
    catch (requestError) { setError(getApiErrorMessage(requestError)); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => {
    const timer = window.setTimeout(loadCart, 0);
    return () => window.clearTimeout(timer);
  }, [loadCart]);

  async function runMutation(action) {
    setBusy(true);
    setError('');
    try { return await action(); }
    catch (requestError) { setError(getApiErrorMessage(requestError)); return null; }
    finally { setBusy(false); }
  }

  async function updateItem(itemId, quantity) {
    const response = await runMutation(() => updateCartItem(token, itemId, quantity));
    if (response) setCart(response);
  }

  async function removeItem(itemId) {
    const response = await runMutation(async () => { await removeCartItem(token, itemId); return getCart(token); });
    if (response) setCart(response);
  }

  async function clear() {
    const response = await runMutation(async () => { await clearCart(token); return getCart(token); });
    if (response) setCart(response);
  }

  return (
    <section>
      <div className="page-heading"><div><p className="kicker">Your selection</p><h1>Shopping cart</h1><p>Prices and availability are refreshed from the store.</p></div>{cart?.items.length > 0 && <button type="button" className="button button--danger" disabled={busy} onClick={clear}>Clear cart</button>}</div>
      {loading && <LoadingState message="Loading your cart…" />}
      {!loading && error && <ErrorState message={error} onRetry={loadCart} />}
      {!loading && !error && cart?.items.length === 0 && <EmptyState title="Your cart is empty" message="Choose a product variant to begin your order." action={<Link className="button button--primary" to="/products">Browse products</Link>} />}
      {!loading && cart?.items.length > 0 && (
        <div className="cart-layout">
          <div className="cart-list">{cart.items.map((item) => <CartRow key={`${item.id}-${item.quantity}`} item={item} currency={cart.currency} busy={busy} onUpdate={updateItem} onRemove={removeItem} />)}</div>
          <aside className="cart-summary"><p className="kicker">Backend-calculated</p><h2>Summary</h2><div><span>Items</span><span>{cart.totalQuantity}</span></div><div><span>Subtotal</span><span>{formatCurrency(cart.subtotal, cart.currency)}</span></div><div className="cart-summary__total"><span>Total</span><strong>{formatCurrency(cart.total, cart.currency)}</strong></div><p className="summary-note">Stock is reserved only during checkout. Checkout is handled by the order component.</p></aside>
        </div>
      )}
    </section>
  );
}
