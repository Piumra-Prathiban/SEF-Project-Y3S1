import { useState } from 'react';
import { formatCurrency } from '../utils/formatters';

export default function ProductCard({ product, onSave, onAddToCart }) {
  const availableVariants = product.variants.filter((variant) => variant.isAvailable);
  const [variantId, setVariantId] = useState(availableVariants[0]?.id ?? product.variants[0]?.id ?? '');
  const [quantity, setQuantity] = useState(1);
  const selectedVariant = product.variants.find((variant) => variant.id === variantId);

  return (
    <article className="product-card">
      <div className="product-card__visual" aria-hidden="true"><span>{product.name.slice(0, 1)}</span></div>
      <div className="product-card__content">
        <div className="eyebrow-row">
          <span>{product.categories.map((category) => category.name).join(' · ') || 'Collection'}</span>
          <span className={product.isAvailable ? 'stock stock--yes' : 'stock stock--no'}>
            {product.isAvailable ? 'In stock' : 'Unavailable'}
          </span>
        </div>
        <h2>{product.name}</h2>
        <p className="product-description">{product.description || 'No description available.'}</p>
        <p className="product-price">From {formatCurrency(product.minimumPrice)}</p>
        <div className="product-options">
          <label>
            Variant
            <select value={variantId} onChange={(event) => setVariantId(event.target.value)}>
              {product.variants.map((variant) => (
                <option key={variant.id} value={variant.id} disabled={!variant.isAvailable}>
                  {variant.name} — {formatCurrency(variant.price)}{!variant.isAvailable ? ' (out of stock)' : ''}
                </option>
              ))}
            </select>
          </label>
          <label className="quantity-field">
            Qty
            <input type="number" min="1" max={selectedVariant?.availableQuantity || 1} value={quantity} onChange={(event) => setQuantity(Number(event.target.value))} />
          </label>
        </div>
        <div className="card-actions">
          <button type="button" className="button button--primary" disabled={!selectedVariant?.isAvailable || quantity < 1} onClick={() => onAddToCart(selectedVariant.id, quantity)}>
            Add to cart
          </button>
          <button type="button" className="button button--secondary" onClick={() => onSave(product.id)}>Save</button>
        </div>
      </div>
    </article>
  );
}
