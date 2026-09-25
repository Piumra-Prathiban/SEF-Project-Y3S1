import { useState } from 'react';
import { MAX_COMMENT_LENGTH } from './reviewUtils';

const STAR_OPTIONS = [1, 2, 3, 4, 5];

/**
 * Star rating and comment form used to write or update a customer's review.
 * Presentational: the parent owns the API call via `onSubmit`.
 */
export function ReviewForm({
  initialRating = 0,
  initialComment = '',
  hasExistingReview = false,
  isSubmitting = false,
  onSubmit,
}) {
  const [rating, setRating] = useState(initialRating);
  const [comment, setComment] = useState(initialComment);
  const [validationError, setValidationError] = useState(null);

  async function handleSubmit(event) {
    event.preventDefault();

    if (rating < 1 || rating > 5) {
      setValidationError('Choose a rating from 1 to 5 stars.');
      return;
    }

    setValidationError(null);

    await onSubmit({ rating, comment });
  }

  return (
    <form className="reviews__form" onSubmit={handleSubmit}>
      <fieldset className="reviews__rating">
        <legend>Your rating</legend>
        <div aria-label="Your rating" className="reviews__rating-options" role="radiogroup">
          {STAR_OPTIONS.map((star) => (
            <button
              aria-label={`${star} star${star === 1 ? '' : 's'}`}
              aria-pressed={star === rating}
              className={star <= rating
                ? 'reviews__star reviews__star--on'
                : 'reviews__star'}
              key={star}
              onClick={() => {
                setRating(star);
                setValidationError(null);
              }}
              type="button"
            >
              ★
            </button>
          ))}
        </div>
      </fieldset>

      <label className="reviews__comment-label" htmlFor="review-comment">
        Your review <span>(optional, up to {MAX_COMMENT_LENGTH} characters)</span>
      </label>
      <textarea
        id="review-comment"
        maxLength={MAX_COMMENT_LENGTH}
        onChange={(event) => setComment(event.target.value)}
        placeholder="Tell other shoppers how the fit, fabric and finish felt."
        rows={4}
        value={comment}
      />

      {validationError && (
        <p className="reviews__validation" role="alert">
          {validationError}
        </p>
      )}

      <button className="button" disabled={isSubmitting} type="submit">
        {isSubmitting
          ? 'Saving...'
          : (hasExistingReview ? 'Update review' : 'Post review')}
      </button>
    </form>
  );
}

export default ReviewForm;
