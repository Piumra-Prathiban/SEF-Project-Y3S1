import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Alert } from '../../components/ui/Alert';
import { ApiErrorAlert } from '../../components/ui/ApiErrorAlert';
import { LoadingState } from '../../components/ui/LoadingState';
import { useAuth } from '../../contexts/AuthContext';
import { ReviewForm } from './ReviewForm';
import {
  deleteOwnReview,
  getOwnReview,
  getProductReviews,
  saveProductReview,
} from './reviewService';
import {
  breakdownPercentage,
  buildBreakdown,
  describeStars,
  formatAverageRating,
  formatReviewDate,
  reviewCountLabel,
} from './reviewUtils';
import './reviews.css';

const EMPTY_AGGREGATE = {
  averageRating: 0,
  totalCount: 0,
  breakdown: { items: [] },
};

const DISPLAY_STARS = [1, 2, 3, 4, 5];

function StarRow({ rating }) {
  return (
    <span aria-hidden="true" className="reviews__star-row">
      {DISPLAY_STARS.map((star) => (
        <span
          className={star <= rating
            ? 'reviews__star reviews__star--on'
            : 'reviews__star'}
          key={star}
        >
          ★
        </span>
      ))}
    </span>
  );
}

function ReviewSummary({ aggregate }) {
  const breakdown = buildBreakdown(aggregate.breakdown?.items);

  return (
    <div className="reviews__summary">
      <div className="reviews__score">
        <span className="reviews__average">
          {formatAverageRating(aggregate.averageRating)}
        </span>
        <span className="reviews__score-meta">
          <span
            className="reviews__stars"
            aria-label={describeStars(Math.round(aggregate.averageRating))}
          >
            <StarRow rating={Math.round(aggregate.averageRating)} />
          </span>
          <span className="reviews__count">
            {reviewCountLabel(aggregate.totalCount)}
          </span>
        </span>
      </div>

      <ul aria-label="Rating breakdown" className="reviews__breakdown">
        {breakdown.map(({ rating, count }) => (
          <li className="reviews__breakdown-row" key={rating}>
            <span className="reviews__breakdown-label">{`${rating} star`}</span>
            <span className="reviews__breakdown-bar">
              <span
                className="reviews__breakdown-fill"
                style={{
                  width: `${breakdownPercentage(count, aggregate.totalCount)}%`,
                }}
              />
            </span>
            <span className="reviews__breakdown-count">{count}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Self-contained ratings and reviews section for a storefront product page.
 * Anonymous visitors can read published reviews; signed-in customers can write,
 * update or delete their own review.
 */
export function ProductReviewsSection({ productId }) {
  const { isAuthenticated, token } = useAuth();

  const [aggregate, setAggregate] = useState(EMPTY_AGGREGATE);
  const [reviews, setReviews] = useState([]);
  const [ownReview, setOwnReview] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [actionError, setActionError] = useState(null);
  const [statusMessage, setStatusMessage] = useState(null);

  const loadReviews = useCallback(async () => {
    setIsLoading(true);
    setError(null);

    try {
      const response = await getProductReviews(productId);

      setAggregate(response?.aggregate ?? EMPTY_AGGREGATE);
      setReviews(response?.reviews ?? []);

      if (isAuthenticated && token) {
        try {
          setOwnReview(await getOwnReview(token, productId));
        } catch {
          // A 404 simply means this customer has not reviewed the product yet.
          setOwnReview(null);
        }
      } else {
        setOwnReview(null);
      }
    } catch (requestError) {
      setError(requestError?.message || 'Reviews could not be loaded.');
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated, productId, token]);

  useEffect(() => {
    // This effect intentionally loads reviews when the product changes.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    loadReviews();
  }, [loadReviews]);

  async function handleSubmit({ rating, comment }) {
    setIsSubmitting(true);
    setActionError(null);
    setStatusMessage(null);

    const trimmedComment = comment.trim();

    try {
      await saveProductReview(token, productId, {
        rating,
        comment: trimmedComment ? trimmedComment : null,
      });

      setStatusMessage(
        ownReview
          ? 'Your review was updated. Thank you for the feedback.'
          : 'Thanks for reviewing this piece.',
      );

      await loadReviews();
    } catch (requestError) {
      setActionError(requestError?.message || 'Your review could not be saved.');
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleDelete() {
    if (!window.confirm('Delete your review for this piece?')) {
      return;
    }

    setIsDeleting(true);
    setActionError(null);
    setStatusMessage(null);

    try {
      await deleteOwnReview(token, productId);
      setStatusMessage('Your review was removed.');
      await loadReviews();
    } catch (requestError) {
      setActionError(requestError?.message || 'Your review could not be removed.');
    } finally {
      setIsDeleting(false);
    }
  }

  return (
    <section aria-labelledby="product-reviews-heading" className="reviews">
      <h2 id="product-reviews-heading">Ratings &amp; reviews</h2>

      <ApiErrorAlert message={error} onRetry={loadReviews} />

      {isLoading ? (
        <LoadingState message="Loading reviews..." />
      ) : (
        !error && (
          <>
            <ReviewSummary aggregate={aggregate} />

            {reviews.length === 0 ? (
              <p className="reviews__empty">
                No reviews yet. Be the first to share your thoughts on this piece.
              </p>
            ) : (
              <ul aria-label="Customer reviews" className="reviews__list">
                {reviews.map((review) => (
                  <li className="reviews__item" key={review.id}>
                    <div className="reviews__item-head">
                      <span className="reviews__author">{review.displayName}</span>
                      <span
                        aria-label={describeStars(review.rating)}
                        className="reviews__stars"
                      >
                        <StarRow rating={review.rating} />
                      </span>
                      <time
                        className="reviews__date"
                        dateTime={review.createdAt}
                      >
                        {formatReviewDate(review.createdAt)}
                      </time>
                    </div>
                    {review.comment && (
                      <p className="reviews__comment">{review.comment}</p>
                    )}
                  </li>
                ))}
              </ul>
            )}
          </>
        )
      )}

      {!error && (
        <div className="reviews__compose">
          {statusMessage && <Alert tone="info">{statusMessage}</Alert>}
          <ApiErrorAlert message={actionError} />

          {isAuthenticated ? (
            <>
              <h3 className="reviews__compose-title">
                {ownReview ? 'Update your review' : 'Write a review'}
              </h3>
              <ReviewForm
                hasExistingReview={Boolean(ownReview)}
                initialComment={ownReview?.comment ?? ''}
                initialRating={ownReview?.rating ?? 0}
                isSubmitting={isSubmitting}
                key={ownReview ? ownReview.id : 'new-review'}
                onSubmit={handleSubmit}
              />
              {ownReview && (
                <button
                  className="button-secondary"
                  disabled={isDeleting}
                  onClick={handleDelete}
                  type="button"
                >
                  {isDeleting ? 'Removing...' : 'Delete my review'}
                </button>
              )}
            </>
          ) : (
            <p className="reviews__signin">
              <Link to="/login">Sign in</Link> to write a review for this piece.
            </p>
          )}
        </div>
      )}
    </section>
  );
}

export default ProductReviewsSection;
