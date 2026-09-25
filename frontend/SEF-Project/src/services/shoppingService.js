import { apiRequest } from './api';

function withQuery(endpoint, query) {
  const search = new URLSearchParams();

  Object.entries(query).forEach(([key, value]) => {
    if (value !== '' && value !== null && value !== undefined) {
      search.set(key, value);
    }
  });

  const queryString = search.toString();
  return queryString ? `${endpoint}?${queryString}` : endpoint;
}

export function searchProducts(query) {
  return apiRequest(withQuery('/shopping/products', query));
}

export function getWishlist(token) {
  return apiRequest('/wishlist', { token });
}

export function addWishlistItem(token, productId) {
  return apiRequest('/wishlist/items', {
    method: 'POST',
    token,
    body: JSON.stringify({ productId }),
  });
}

export function removeWishlistItem(token, productId) {
  return apiRequest(`/wishlist/items/${productId}`, {
    method: 'DELETE',
    token,
  });
}

export function getCart(token) {
  return apiRequest('/cart', { token });
}

export function addCartItem(token, productVariantId, quantity) {
  return apiRequest('/cart/items', {
    method: 'POST',
    token,
    body: JSON.stringify({ productVariantId, quantity }),
  });
}

export function updateCartItem(token, itemId, quantity) {
  return apiRequest(`/cart/items/${itemId}`, {
    method: 'PUT',
    token,
    body: JSON.stringify({ quantity }),
  });
}

export function removeCartItem(token, itemId) {
  return apiRequest(`/cart/items/${itemId}`, {
    method: 'DELETE',
    token,
  });
}

export function clearCart(token) {
  return apiRequest('/cart', {
    method: 'DELETE',
    token,
  });
}

export function getProfile(token) {
  return apiRequest('/profile', { token });
}

export function updateProfile(token, profile) {
  return apiRequest('/profile', {
    method: 'PUT',
    token,
    body: JSON.stringify(profile),
  });
}

export function getAddresses(token) {
  return apiRequest('/profile/addresses', { token });
}

export function createAddress(token, address) {
  return apiRequest('/profile/addresses', {
    method: 'POST',
    token,
    body: JSON.stringify(address),
  });
}

export function updateAddress(token, addressId, address) {
  return apiRequest(`/profile/addresses/${addressId}`, {
    method: 'PUT',
    token,
    body: JSON.stringify(address),
  });
}

export function deleteAddress(token, addressId) {
  return apiRequest(`/profile/addresses/${addressId}`, {
    method: 'DELETE',
    token,
  });
}
