import { useCallback, useEffect, useState } from 'react';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../contexts/AuthContext';
import {
  deleteReview,
  listReviewsForModeration,
  setReviewPublished,
} from './reviewService';
import { describeStars, formatReviewDate } from './reviewUtils';
import './reviews.css';

const PAGE_SIZE = 10;
const RATING_OPTIONS = [5, 4, 3, 2, 1];
const DEFAULT_FILTERS = { productId: '', published: 'all', minRating: 'all' };

/**
 * Staff-only review moderation: filter reviews, hide or unhide them from the
 * storefront, and delete any review. Intended for a staff page.
 */
export function ReviewsModerationSection() {
  const { token } = useAuth();

  const [filters, setFilters] = useState(DEFAULT_FILTERS);
  const [page, setPage] = useState(1);
  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [busyId, setBusyId] = useState('');
  const [actionError, setActionError] = useState(null);
  const [statusMessage, setStatusMessage] = useState(null);

  const loadReviews = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await listReviewsForModeration(token, {
        productId: filters.productId.trim() || undefined,
        isPublished:
          filters.published === 'all'
            ? undefined
            : filters.published === 'published',
        minRating:
          filters.minRating === 'all' ? undefined : Number(filters.minRating),
        page,
        pageSize: PAGE_SIZE,
      });

      setData(response);
    } catch (requestError) {
      setError(requestError?.message || 'Reviews could not be loaded.');
    } finally {
      setIsLoading(false);
    }
  }, [filters, page, token]);

  useEffect(() => {
    // This effect intentionally reloads whenever the filters or page change.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadReviews();
  }, [loadReviews]);

  function updateFilter(name, value) {
    setFilters((current) => ({ ...current, [name]: value }));
    setPage(1);
  }

  async function togglePublished(review) {
    setBusyId(review.id);
    setActionError(null);
    setStatusMessage(null);

    try {
      const updated = await setReviewPublished(token, review.id, !review.isPublished);

      setData((current) => (current
        ? {
          ...current,
          items: current.items.map((item) => (
            item.id === updated.id ? updated : item
          )),
        }
        : current));

      setStatusMessage(
        updated.isPublished
          ? 'Review is now visible in the storefront.'
          : 'Review hidden from the storefront.',
      );
    } catch (requestError) {
      setActionError(requestError?.message || 'The review could not be updated.');
    } finally {
      setBusyId('');
    }
  }

  async function removeReview(review) {
    if (!window.confirm('Delete this review permanently?')) {
      return;
    }

    setBusyId(review.id);
    setActionError(null);
    setStatusMessage(null);

    try {
      await deleteReview(token, review.id);

      setData((current) => (current
        ? {
          ...current,
          items: current.items.filter((item) => item.id !== review.id),
          totalItems: Math.max(0, current.totalItems - 1),
        }
        : current));

      setStatusMessage('Review deleted.');
    } catch (requestError) {
      setActionError(requestError?.message || 'The review could not be deleted.');
    } finally {
      setBusyId('');
    }
  }

  const items = data?.items ?? [];

  return (
    <section
      aria-labelledby="reviews-moderation-heading"
      className="reviews reviews--moderation"
    >
      <h2 id="reviews-moderation-heading">Review moderation</h2>

      <form className="reviews__filters" onSubmit={(event) => event.preventDefault()}>
        <label className="reviews__filter" htmlFor="reviews-filter-product">
          Product ID
          <input
            id="reviews-filter-product"
            onChange={(event) => updateFilter('productId', event.target.value)}
            placeholder="Filter by product id"
            type="text"
            value={filters.productId}
          />
        </label>

        <label className="reviews__filter" htmlFor="reviews-filter-published">
          Visibility
          <select
            id="reviews-filter-published"
            onChange={(event) => updateFilter('published', event.target.value)}
            value={filters.published}
          >
            <option value="all">All reviews</option>
            <option value="published">Published only</option>
            <option value="hidden">Hidden only</option>
          </select>
        </label>

        <label className="reviews__filter" htmlFor="reviews-filter-rating">
          Minimum rating
          <select
            id="reviews-filter-rating"
            onChange={(event) => updateFilter('minRating', event.target.value)}
            value={filters.minRating}
          >
            <option value="all">Any rating</option>
            {RATING_OPTIONS.map((rating) => (
              <option key={rating} value={rating}>
                {rating} stars and up
              </option>
            ))}
          </select>
        </label>
      </form>

      <ApiErrorAlert message={error} onRetry={loadReviews} />
      {statusMessage && <Alert tone="info">{statusMessage}</Alert>}
      <ApiErrorAlert message={actionError} />

      {isLoading ? (
        <LoadingState message="Loading reviews..." />
      ) : (
        !error
        && (items.length === 0 ? (
          <p className="reviews__empty">No reviews match these filters.</p>
        ) : (
          <table className="reviews__table">
            <thead>
              <tr>
                <th scope="col">Product</th>
                <th scope="col">Reviewer</th>
                <th scope="col">Rating</th>
                <th scope="col">Review</th>
                <th scope="col">Status</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((review) => (
                <tr key={review.id}>
                  <td className="reviews__table-product">{review.productId}</td>
                  <td>{review.displayName}</td>
                  <td>
                    <span aria-label={describeStars(review.rating)}>
                      {review.rating} / 5
                    </span>
                  </td>
                  <td className="reviews__table-comment">
                    {review.comment || '—'}
                    <span className="reviews__table-date">
                      {formatReviewDate(review.createdAt)}
                    </span>
                  </td>
                  <td>{review.isPublished ? 'Published' : 'Hidden'}</td>
                  <td className="reviews__table-actions">
                    <button
                      className="button-secondary"
                      disabled={busyId === review.id}
                      onClick={() => togglePublished(review)}
                      type="button"
                    >
                      {review.isPublished ? 'Hide' : 'Unhide'}
                    </button>
                    <button
                      className="button-secondary"
                      disabled={busyId === review.id}
                      onClick={() => removeReview(review)}
                      type="button"
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ))
      )}

      {data && data.totalPages > 1 && (
        <div className="reviews__pagination">
          <button
            className="button-secondary"
            disabled={page <= 1}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
            type="button"
          >
            Previous
          </button>
          <span>
            Page {data.page} of {data.totalPages}
          </span>
          <button
            className="button-secondary"
            disabled={page >= data.totalPages}
            onClick={() => setPage((current) => current + 1)}
            type="button"
          >
            Next
          </button>
        </div>
      )}
    </section>
  );
}

export default ReviewsModerationSection;
