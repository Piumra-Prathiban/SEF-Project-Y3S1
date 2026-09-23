import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState';
import { getApiErrorMessage } from '../services/api';
import { getWishlist, removeWishlistItem } from '../services/shoppingService';
import { formatCurrency } from '../utils/formatters';

export default function WishlistPage() {
  const { token } = useAuth();
  const [wishlist, setWishlist] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busyProductId, setBusyProductId] = useState('');

  const loadWishlist = useCallback(async () => {
    await Promise.resolve();
    setLoading(true);
    setError('');
    try { setWishlist(await getWishlist(token)); }
    catch (requestError) { setError(getApiErrorMessage(requestError)); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => {
    const timer = window.setTimeout(loadWishlist, 0);
    return () => window.clearTimeout(timer);
  }, [loadWishlist]);

  async function removeProduct(productId) {
    setBusyProductId(productId);
    try {
      await removeWishlistItem(token, productId);
      setWishlist((current) => ({ ...current, items: current.items.filter((item) => item.productId !== productId), count: current.count - 1 }));
    } catch (requestError) {
      setError(getApiErrorMessage(requestError));
    } finally {
      setBusyProductId('');
    }
  }

  return (
    <section>
      <div className="page-heading"><div><p className="kicker">Saved for later</p><h1>Your wishlist</h1><p>Keep products here until you are ready to choose a variant.</p></div><Link className="button button--secondary" to="/products">Continue shopping</Link></div>
      {loading && <LoadingState message="Loading your wishlist…" />}
      {!loading && error && <ErrorState message={error} onRetry={loadWishlist} />}
      {!loading && !error && wishlist?.items.length === 0 && <EmptyState title="Your wishlist is empty" message="Save products while browsing and they will appear here." action={<Link className="button button--primary" to="/products">Browse products</Link>} />}
      {!loading && !error && wishlist?.items.length > 0 && (
        <div className="saved-grid">
          {wishlist.items.map((item) => (
            <article className="saved-card" key={item.productId}>
              <div className="saved-card__visual" aria-hidden="true">{item.productName.slice(0, 1)}</div>
              <div className="saved-card__body">
                <div className="eyebrow-row"><span>Saved product</span><span className={item.isAvailable ? 'stock stock--yes' : 'stock stock--no'}>{item.isAvailable ? 'Available' : 'Unavailable'}</span></div>
                <h2>{item.productName}</h2>
                <p>{item.description || 'No description available.'}</p>
                <p className="product-price">{item.minimumPrice == null ? 'No active variants' : `From ${formatCurrency(item.minimumPrice)}`}</p>
                <div className="card-actions">
                  <Link className="button button--primary" to={`/products?search=${encodeURIComponent(item.productName)}`}>Choose variant</Link>
                  <button type="button" className="button button--danger" disabled={busyProductId === item.productId} onClick={() => removeProduct(item.productId)}>{busyProductId === item.productId ? 'Removing…' : 'Remove'}</button>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
