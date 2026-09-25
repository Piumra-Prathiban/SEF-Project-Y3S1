import { Link } from 'react-router-dom';
import { formatCurrency } from '../../utils/format';
import { resolveImageUrl } from '../../utils/images';

function getInitials(name) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0])
    .join('')
    .toUpperCase();
}

function getHue(value) {
  let hash = 0;

  for (let index = 0; index < value.length; index += 1) {
    hash = (hash * 31 + value.charCodeAt(index)) % 360;
  }

  return hash;
}

export function ProductCard({ product }) {
  const hue = getHue(product.name);
  const hasPriceRange = product.priceTo > product.priceFrom;
  const priceLabel = hasPriceRange
    ? `From ${formatCurrency(product.priceFrom, 'LKR')}`
    : formatCurrency(product.priceFrom, 'LKR');

  const colours = product.colours ?? [];
  const imageUrl = resolveImageUrl(product.imageUrl);

  return (
    <article className="product-card">
      <div
        aria-hidden="true"
        className="product-card__media"
        style={{
          background: `linear-gradient(140deg, hsl(${hue} 72% 93%), hsl(${hue} 52% 78%))`,
        }}
      >
        {imageUrl ? (
          <img alt="" className="product-card__image" src={imageUrl} />
        ) : (
          <span className="product-card__initials">
            {getInitials(product.name)}
          </span>
        )}
      </div>

      <div className="product-card__body">
        <p className="product-card__meta">
          {product.categoryName}
          {product.collectionName ? ` · ${product.collectionName}` : ''}
        </p>
        <h3 className="product-card__name">
          <Link to={`/shop/${product.id}`}>{product.name}</Link>
        </h3>
        <p className="product-card__price">{priceLabel}</p>

        {(product.sizes ?? []).length > 0 && (
          <p className="product-card__options">
            {(product.sizes ?? []).join(' · ')}
          </p>
        )}

        {colours.length > 0 && (
          <div className="product-card__colours">
            {colours.map((colour) => (
              <span className="product-card__colour" key={colour.name}>
                <span
                  aria-hidden="true"
                  className="product-card__colour-dot"
                  style={{ background: colour.hexCode ?? 'var(--border)' }}
                />
                {colour.name}
              </span>
            ))}
          </div>
        )}

        {product.inStock ? (
          <Link className="product-card__buy" to={`/shop/${product.id}`}>
            Choose options
          </Link>
        ) : (
          <p className="product-card__sold-out">Sold out</p>
        )}
      </div>
    </article>
  );
}

export default ProductCard;
