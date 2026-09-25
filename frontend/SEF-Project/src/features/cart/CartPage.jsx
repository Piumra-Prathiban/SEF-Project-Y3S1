import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { formatCurrency } from '../../utils/format';
import { resolveImageUrl } from '../../utils/images';
import { useCart } from './CartContext';
import '../storefront/storefront.css';
import './cart.css';

function getInitials(name) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0])
    .join('')
    .toUpperCase();
}

function CartItemMedia({ imageUrl, productName }) {
  const resolvedUrl = resolveImageUrl(imageUrl);

  return (
    <div aria-hidden="true" className="cart-item__media">
      {resolvedUrl ? (
        <img alt="" src={resolvedUrl} />
      ) : (
        <span>{getInitials(productName)}</span>
      )}
    </div>
  );
}

function CartQuantityInput({ item, onUpdate }) {
  const [draft, setDraft] = useState(String(item.quantity));

  function handleChange(event) {
    const value = event.target.value;
    const parsed = Number(value);

    setDraft(value);

    if (Number.isInteger(parsed) && parsed >= 1) {
      onUpdate(item.variantId, parsed);
    }
  }

  function handleBlur() {
    const parsed = Number(draft);

    if (!Number.isInteger(parsed) || parsed < 1) {
      setDraft(String(item.quantity));
    }
  }

  return (
    <input
      id={`quantity-${item.variantId}`}
      min="1"
      onBlur={handleBlur}
      onChange={handleChange}
      type="number"
      value={draft}
    />
  );
}

export function CartPage() {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();
  const {
    items,
    itemCount,
    estimatedSubtotal,
    updateQuantity,
    removeItem,
  } = useCart();

  function handleCheckout() {
    // Checkout needs an account; signing in returns here through the dashboard.
    navigate(isAuthenticated ? '/checkout' : '/login');
  }

  return (
    <div className="storefront">
      <div className="storefront__catalogue">
        <h1>Your cart</h1>

        {items.length === 0 ? (
          <div className="empty-state">
            <p>Your cart is empty.</p>
            <Link className="storefront__cta" to="/">
              Shop the collection
            </Link>
          </div>
        ) : (
          <>
            <ul className="cart-list">
              {items.map((item) => (
                <li className="cart-item" key={item.variantId}>
                  <CartItemMedia
                    imageUrl={item.imageUrl}
                    productName={item.productName}
                  />

                  <div className="cart-item__details">
                    <Link
                      className="cart-item__name"
                      to={`/shop/${item.productId}`}
                    >
                      {item.productName}
                    </Link>
                    <p className="cart-item__meta">
                      {item.sizeName} · {item.colourName}
                    </p>
                    <button
                      className="cart-item__remove"
                      onClick={() => removeItem(item.variantId)}
                      type="button"
                    >
                      Remove
                    </button>
                  </div>

                  <div className="cart-item__quantity">
                    <label htmlFor={`quantity-${item.variantId}`}>Qty</label>
                    <CartQuantityInput item={item} onUpdate={updateQuantity} />
                  </div>

                  <p className="cart-item__price">
                    {formatCurrency(item.price * item.quantity, 'LKR')}
                  </p>
                </li>
              ))}
            </ul>

            <div className="cart-summary">
              <p>
                Estimated subtotal ({itemCount}{' '}
                {itemCount === 1 ? 'item' : 'items'}):{' '}
                <strong>{formatCurrency(estimatedSubtotal, 'LKR')}</strong>
              </p>
              <p className="cart-summary__note">
                Taxes and shipping are calculated by the server when the order
                is placed.
              </p>
              <button onClick={handleCheckout} type="button">
                Proceed to checkout
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

export default CartPage;
