import { apiRequest } from '../../services/api.js';

/**
 * @typedef {Object} ReviewDto
 * @property {string} id
 * @property {string} productId
 * @property {string} displayName
 * @property {number} rating
 * @property {string|null} comment
 * @property {boolean} isPublished
 * @property {string} createdAt
 * @property {string} updatedAt
 *
 * @typedef {Object} ReviewAggregateDto
 * @property {number} averageRating
 * @property {number} totalCount
 * @property {{ items: Array<{rating: number, count: number}> }} breakdown
 *
 * @typedef {Object} ProductReviewsDto
 * @property {string} productId
 * @property {ReviewAggregateDto} aggregate
 * @property {ReviewDto[]} reviews
 *
 * @typedef {Object} StaffReviewListDto
 * @property {ReviewDto[]} items
 * @property {number} page
 * @property {number} pageSize
 * @property {number} totalItems
 * @property {number} totalPages
 */

/**
 * Public read of a product's published reviews and aggregate rating.
 * @returns {Promise<ProductReviewsDto>}
 */
export function getProductReviews(productId) {
  return apiRequest(`/reviews/products/${productId}`);
}

/** Returns the signed-in customer's own review, or rejects with a 404 status. */
export function getOwnReview(token, productId) {
  return apiRequest(`/reviews/products/${productId}/mine`, { token });
}

/**
 * Creates or updates the signed-in customer's review of a product.
 * @returns {Promise<ReviewDto>}
 */
export function saveProductReview(token, productId, { rating, comment }) {
  return apiRequest(`/reviews/products/${productId}`, {
    method: 'PUT',
    token,
    body: JSON.stringify({ rating, comment }),
  });
}

/** Deletes the signed-in customer's own review for a product. */
export function deleteOwnReview(token, productId) {
  return apiRequest(`/reviews/products/${productId}`, {
    method: 'DELETE',
    token,
  });
}

/**
 * Staff moderation: paged list of reviews filtered by product, published state
 * and minimum rating.
 * @returns {Promise<StaffReviewListDto>}
 */
export function listReviewsForModeration(token, query = {}) {
  return apiRequest('/reviews', { token, query });
}

/** Staff moderation: hide or unhide a review. */
export function setReviewPublished(token, reviewId, isPublished) {
  return apiRequest(`/reviews/${reviewId}/published`, {
    method: 'PATCH',
    token,
    body: JSON.stringify({ isPublished }),
  });
}

/** Staff moderation: delete any review. */
export function deleteReview(token, reviewId) {
  return apiRequest(`/reviews/${reviewId}`, { method: 'DELETE', token });
}
