/** Maximum length of a written review, mirroring the API constraint. */
export const MAX_COMMENT_LENGTH = 2000;

/** Lowest-to-highest list of every star rating a review can carry. */
export const STAR_VALUES = [5, 4, 3, 2, 1];

/** Formats an average rating to a single decimal place (e.g. 4.5). */
export function formatAverageRating(average) {
  const value = Number(average);

  return Number.isFinite(value) ? value.toFixed(1) : '0.0';
}

/** Formats a review timestamp for display, or an empty string if invalid. */
export function formatReviewDate(value) {
  if (!value) {
    return '';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return date.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

/** Human-readable, screen-reader friendly description of a star rating. */
export function describeStars(rating) {
  const value = Number(rating) || 0;

  return `${value} out of 5 star${value === 1 ? '' : 's'}`;
}

/**
 * Normalises the API breakdown into a stable 5-to-1 list, filling any missing
 * star rating with a zero count.
 */
export function buildBreakdown(breakdownItems) {
  const counts = new Map(
    (breakdownItems ?? []).map((item) => [item.rating, item.count]),
  );

  return STAR_VALUES.map((rating) => ({
    rating,
    count: counts.get(rating) ?? 0,
  }));
}

/** Percentage of the total a single star count represents (0 when empty). */
export function breakdownPercentage(count, totalCount) {
  if (!totalCount) {
    return 0;
  }

  return Math.round((count / totalCount) * 100);
}

/** A grammatically correct label such as "1 review" or "3 reviews". */
export function reviewCountLabel(totalCount) {
  const count = Number(totalCount) || 0;

  return `${count} ${count === 1 ? 'review' : 'reviews'}`;
}
