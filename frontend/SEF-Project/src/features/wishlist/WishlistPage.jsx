import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { PageShell } from '../../components/ui/PageShell';
import { useAuth } from '../../contexts/AuthContext';
import { formatCurrency } from '../../utils/format';
import { getWishlist, removeWishlistItem } from './wishlistService';

export function WishlistPage() {
  const { token } = useAuth();

  const [wishlist, setWishlist] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [busyProductId, setBusyProductId] = useState('');

  const loadWishlist = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      setWishlist(await getWishlist(token));
    } catch (requestError) {
      setError(requestError?.message || 'Your wishlist could not be loaded.');
    } finally {
      setIsLoading(false);
    }
  }, [token]);

  useEffect(() => {
    // This effect intentionally loads the wishlist for the signed-in customer.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadWishlist();
  }, [loadWishlist]);

  async function removeProduct(productId) {
    setBusyProductId(productId);
    setError(null);

    try {
      await removeWishlistItem(token, productId);
      setWishlist((current) => ({
        ...current,
        items: current.items.filter((item) => item.productId !== productId),
        count: current.count - 1,
      }));
    } catch (requestError) {
      setError(requestError?.message || 'The product could not be removed.');
    } finally {
      setBusyProductId('');
    }
  }

  const items = wishlist?.items ?? [];

  return (
    <PageShell
      description="Pieces you saved while browsing. Pick a size and colour when you are ready."
      eyebrow="Clothic · Saved"
      title="Wishlist"
    >
      <ApiErrorAlert message={error} onRetry={loadWishlist} />

      {isLoading ? (
        <LoadingState message="Loading your wishlist..." />
      ) : !error && items.length === 0 ? (
        <div className="empty-state">
          <p>Your wishlist is empty.</p>
          <Link className="button" to="/">
            Browse the collection
          </Link>
        </div>
      ) : (
        <div className="saved-grid">
          {items.map((item) => (
            <article className="saved-card" key={item.productId}>
              <div aria-hidden="true" className="saved-card__visual">
                {item.productName.slice(0, 1)}
              </div>

              <div className="saved-card__body">
                <div className="eyebrow-row">
                  <span>Saved piece</span>
                  <span className={item.isAvailable ? 'stock stock--yes' : 'stock stock--no'}>
                    {item.isAvailable ? 'Available' : 'Unavailable'}
                  </span>
                </div>

                <h2>{item.productName}</h2>
                <p>{item.description || 'No description available.'}</p>
                <p className="product-price">
                  {item.minimumPrice == null
                    ? 'No active variants'
                    : `From ${formatCurrency(item.minimumPrice, 'LKR')}`}
                </p>

                <div className="card-actions">
                  <Link className="button" to={`/shop/${item.productId}`}>
                    Choose options
                  </Link>
                  <button
                    className="button-secondary"
                    disabled={busyProductId === item.productId}
                    onClick={() => removeProduct(item.productId)}
                    type="button"
                  >
                    {busyProductId === item.productId ? 'Removing...' : 'Remove'}
                  </button>
                </div>
              </div>
            </article>
          ))}
        </div>
      )}
    </PageShell>
  );
}

export default WishlistPage;
