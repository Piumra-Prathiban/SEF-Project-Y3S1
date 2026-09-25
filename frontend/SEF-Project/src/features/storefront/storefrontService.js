import { apiRequest } from '../../services/api.js';

// Public storefront endpoints: no token is sent, so the homepage works for
// visitors who are not signed in yet.
export async function getStorefrontProducts({ search, categoryId, limit } = {}) {
  const params = new URLSearchParams();

  if (search) {
    params.set('search', search);
  }

  if (categoryId) {
    params.set('categoryId', categoryId);
  }

  if (limit) {
    params.set('limit', limit);
  }

  const queryString = params.toString();

  return apiRequest(
    `/storefront/products${queryString ? `?${queryString}` : ''}`,
    { method: 'GET' },
  );
}

export async function getStorefrontProduct(productId) {
  return apiRequest(`/storefront/products/${productId}`, { method: 'GET' });
}
