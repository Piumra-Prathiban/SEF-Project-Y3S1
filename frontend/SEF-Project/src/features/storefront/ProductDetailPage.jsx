import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../contexts/AuthContext';
import { formatCurrency } from '../../utils/format';
import { resolveImageUrl } from '../../utils/images';
import { useCart } from '../cart/CartContext';
import { addWishlistItem } from '../wishlist/wishlistService';
import { ProductReviewsSection } from '../reviews';
import { getStorefrontProduct } from './storefrontService';
import './storefront.css';
import './productDetail.css';

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

export function ProductDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated, token } = useAuth();
  const { addItem } = useCart();

  const [product, setProduct] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [notFound, setNotFound] = useState(false);
  const [selectedSize, setSelectedSize] = useState('');
  const [selectedColour, setSelectedColour] = useState('');
  const [quantity, setQuantity] = useState(1);
  const [notice, setNotice] = useState(null);
  const [isSavingToWishlist, setIsSavingToWishlist] = useState(false);

  const loadProduct = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    setNotFound(false);

    try {
      const response = await getStorefrontProduct(id);
      const variants = response?.variants ?? [];
      const preferred = variants.find((variant) => variant.inStock) ?? variants[0];

      setProduct(response);
      setSelectedSize(preferred?.sizeName ?? '');
      setSelectedColour(preferred?.colourName ?? '');
      setQuantity(1);
    } catch (err) {
      if (err?.status === 404) {
        setNotFound(true);
      } else {
        setError(err?.message || 'The product could not be loaded.');
      }
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  useEffect(() => {
    // This effect intentionally loads the product when the route changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadProduct();
  }, [loadProduct]);

  const variants = useMemo(() => product?.variants ?? [], [product]);

  const sizes = useMemo(
    () => [...new Set(variants.map((variant) => variant.sizeName))],
    [variants],
  );

  const coloursForSize = useMemo(() => {
    const byName = new Map();

    variants
      .filter((variant) => variant.sizeName === selectedSize)
      .forEach((variant) => byName.set(variant.colourName, variant.colourHex));

    return [...byName.entries()].map(([name, hex]) => ({ name, hex }));
  }, [variants, selectedSize]);

  const selectedVariant = variants.find(
    (variant) => variant.sizeName === selectedSize
      && variant.colourName === selectedColour,
  ) ?? null;

  function handleSizeChange(sizeName) {
    setSelectedSize(sizeName);
    setNotice(null);

    const firstColour = variants.find(
      (variant) => variant.sizeName === sizeName,
    );

    setSelectedColour(firstColour?.colourName ?? '');
  }

  function handleAddToCart({ buyNow }) {
    if (!selectedVariant || !selectedVariant.inStock) {
      return;
    }

    addItem({
      variantId: selectedVariant.id,
      productId: product.id,
      productName: product.name,
      imageUrl: product.imageUrl ?? null,
      sizeName: selectedVariant.sizeName,
      colourName: selectedVariant.colourName,
      price: selectedVariant.price,
      quantity,
    });

    if (buyNow) {
      // Checkout needs an account; browsing and the cart itself do not.
      navigate(isAuthenticated ? '/cart' : '/login', isAuthenticated ? undefined : { state: { from: { pathname: '/cart', search: '' } } });
      return;
    }

    setNotice(
      `${product.name} (${selectedVariant.sizeName} / ${selectedVariant.colourName}) added to your cart.`,
    );
  }

  async function handleSaveToWishlist() {
    if (!isAuthenticated) {
      navigate('/login');
      return;
    }

    setIsSavingToWishlist(true);
    setNotice(null);

    try {
      await addWishlistItem(token, product.id);
      setNotice(`${product.name} was saved to your wishlist.`);
    } catch (err) {
      setNotice(
        err?.status === 409
          ? `${product.name} is already in your wishlist.`
          : (err?.message || 'The product could not be saved to your wishlist.'),
      );
    } finally {
      setIsSavingToWishlist(false);
    }
  }

  if (isLoading) {
    return (
      <div className="storefront">
        <div className="storefront__catalogue">
          <LoadingState message="Loading the product..." />
        </div>
      </div>
    );
  }

  if (notFound) {
    return (
      <div className="storefront">
        <div className="storefront__catalogue">
          <h1>Product not found</h1>
          <p className="storefront__lede">
            This piece is no longer in the collection.
          </p>
          <Link className="storefront__cta" to="/">
            Back to the collection
          </Link>
        </div>
      </div>
    );
  }

  if (error || !product) {
    return (
      <div className="storefront">
        <div className="storefront__catalogue">
          <ApiErrorAlert message={error} onRetry={loadProduct} />
        </div>
      </div>
    );
  }

  const hue = getHue(product.name);
  const price = selectedVariant?.price ?? product.priceFrom;
  const imageUrl = resolveImageUrl(product.imageUrl);

  return (
    <div className="storefront">
      <div className="storefront__catalogue">
        <Link className="product-detail__back" to="/">
          Back to the collection
        </Link>

        <article className="product-detail">
          <div className="product-detail__media">
            {imageUrl ? (
              <img alt={product.name} src={imageUrl} />
            ) : (
              <div
                aria-hidden="true"
                className="product-detail__placeholder"
                style={{
                  background: `linear-gradient(140deg, hsl(${hue} 72% 93%), hsl(${hue} 52% 78%))`,
                }}
              >
                <span className="product-card__initials">
                  {getInitials(product.name)}
                </span>
              </div>
            )}
          </div>

          <div className="product-detail__info">
            <p className="product-card__meta">
              {product.categoryName}
              {product.collectionName ? ` · ${product.collectionName}` : ''}
            </p>
            <h1>{product.name}</h1>
            <p className="product-detail__price">
              {formatCurrency(price, 'LKR')}
            </p>

            {product.description && (
              <p className="product-detail__description">{product.description}</p>
            )}

            <div className="product-detail__option">
              <label htmlFor="product-size">Size</label>
              <select
                id="product-size"
                onChange={(event) => handleSizeChange(event.target.value)}
                value={selectedSize}
              >
                {sizes.map((size) => (
                  <option key={size} value={size}>
                    {size}
                  </option>
                ))}
              </select>
            </div>

            <div className="product-detail__option">
              <span className="product-detail__option-label">Colour</span>
              <div className="product-detail__swatches">
                {coloursForSize.map((colour) => (
                  <button
                    aria-pressed={colour.name === selectedColour}
                    className="product-detail__swatch"
                    key={colour.name}
                    onClick={() => {
                      setSelectedColour(colour.name);
                      setNotice(null);
                    }}
                    type="button"
                  >
                    <span
                      aria-hidden="true"
                      className="product-detail__swatch-dot"
                      style={{ background: colour.hex ?? 'var(--border)' }}
                    />
                    {colour.name}
                  </button>
                ))}
              </div>
            </div>

            <div className="product-detail__option">
              <label htmlFor="product-quantity">Quantity</label>
              <input
                id="product-quantity"
                min="1"
                onChange={(event) => setQuantity(Math.max(1, Number(event.target.value)))}
                type="number"
                value={quantity}
              />
            </div>

            <p className="product-detail__stock">
              {selectedVariant
                ? (selectedVariant.inStock
                  ? 'In stock'
                  : 'Sold out in this size and colour')
                : 'Select a size and colour'}
            </p>

            {notice && <Alert>{notice}</Alert>}

            <div className="product-detail__actions">
              <button
                disabled={!selectedVariant || !selectedVariant.inStock}
                onClick={() => handleAddToCart({ buyNow: false })}
                type="button"
              >
                Add to cart
              </button>
              <button
                className="product-detail__buy"
                disabled={!selectedVariant || !selectedVariant.inStock}
                onClick={() => handleAddToCart({ buyNow: true })}
                type="button"
              >
                Buy now
              </button>
              <button
                className="button-secondary"
                disabled={isSavingToWishlist}
                onClick={handleSaveToWishlist}
                type="button"
              >
                {isSavingToWishlist ? 'Saving...' : 'Save to wishlist'}
              </button>
            </div>
          </div>
        </article>

        <ProductReviewsSection productId={product.id} />
      </div>
    </div>
  );
}

export default ProductDetailPage;
