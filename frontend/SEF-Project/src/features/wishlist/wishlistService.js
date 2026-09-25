import { apiRequest } from '../../services/api.js';

/**
 * @typedef {Object} WishlistItemDto
 * @property {string} productId
 * @property {string} productName
 * @property {string|null} description
 * @property {number|null} minimumPrice
 * @property {boolean} isProductActive
 * @property {boolean} isAvailable
 * @property {string} addedAt
 *
 * @typedef {Object} WishlistDto
 * @property {string|null} id
 * @property {WishlistItemDto[]} items
 * @property {number} count
 */

/** @returns {Promise<WishlistDto>} */
export function getWishlist(token) {
  return apiRequest('/wishlist', { token });
}

/** @returns {Promise<WishlistItemDto>} */
export function addWishlistItem(token, productId) {
  return apiRequest('/wishlist/items', {
    method: 'POST',
    token,
    body: JSON.stringify({ productId }),
  });
}

/** @returns {Promise<void>} */
export function removeWishlistItem(token, productId) {
  return apiRequest(`/wishlist/items/${productId}`, {
    method: 'DELETE',
    token,
  });
}

/** @returns {Promise<{count: number}>} */
export function getWishlistCount(token) {
  return apiRequest('/wishlist/count', { token });
}
