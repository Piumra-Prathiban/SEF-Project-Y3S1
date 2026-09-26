import { apiRequest } from './api.js';

function jsonRequest(endpoint, method, data, options = {}) {
  return apiRequest(endpoint, {
    ...options,
    method,
    body: JSON.stringify(data),
  });
}

function deleteRequest(endpoint, options = {}) {
  return apiRequest(endpoint, {
    ...options,
    method: 'DELETE',
  });
}

function splitQueryAndOptions(query, options) {
  const queryLooksLikeOptions =
    query
    && typeof query === 'object'
    && !Array.isArray(query)
    && options === undefined
    && ('token' in query || 'headers' in query || 'signal' in query);

  if (queryLooksLikeOptions) {
    return { query: undefined, options: query };
  }

  return { query, options };
}

export function getProducts(query, options) {
  return apiRequest('/products', {
    ...options,
    method: 'GET',
    query,
  });
}

export function getProduct(id, options) {
  return apiRequest(`/products/${id}`, {
    ...options,
    method: 'GET',
  });
}

export function createProduct(product, options) {
  return jsonRequest('/products', 'POST', product, options);
}

export function updateProduct(id, product, options) {
  return jsonRequest(`/products/${id}`, 'PUT', product, options);
}

export function deleteProduct(id, options) {
  return deleteRequest(`/products/${id}`, options);
}

export function getCategories(options) {
  return apiRequest('/categories', {
    ...options,
    method: 'GET',
  });
}

export function getCategory(id, options) {
  return apiRequest(`/categories/${id}`, {
    ...options,
    method: 'GET',
  });
}

export function createCategory(category, options) {
  return jsonRequest('/categories', 'POST', category, options);
}

export function updateCategory(id, category, options) {
  return jsonRequest(`/categories/${id}`, 'PUT', category, options);
}

export function deleteCategory(id, options) {
  return deleteRequest(`/categories/${id}`, options);
}

export function getSuppliers(options) {
  return apiRequest('/suppliers', {
    ...options,
    method: 'GET',
  });
}

export function getSupplier(id, options) {
  return apiRequest(`/suppliers/${id}`, {
    ...options,
    method: 'GET',
  });
}

export function createSupplier(supplier, options) {
  return jsonRequest('/suppliers', 'POST', supplier, options);
}

export function updateSupplier(id, supplier, options) {
  return jsonRequest(`/suppliers/${id}`, 'PUT', supplier, options);
}

export function deleteSupplier(id, options) {
  return deleteRequest(`/suppliers/${id}`, options);
}

export function getCollections(options) {
  return apiRequest('/collections', {
    ...options,
    method: 'GET',
  });
}

export function getCollection(id, options) {
  return apiRequest(`/collections/${id}`, {
    ...options,
    method: 'GET',
  });
}

export function createCollection(collection, options) {
  return jsonRequest('/collections', 'POST', collection, options);
}

export function updateCollection(id, collection, options) {
  return jsonRequest(`/collections/${id}`, 'PUT', collection, options);
}

export function deleteCollection(id, options) {
  return deleteRequest(`/collections/${id}`, options);
}

export function getSizes(options) {
  return apiRequest('/sizes', {
    ...options,
    method: 'GET',
  });
}

export function createSize(size, options) {
  return jsonRequest('/sizes', 'POST', size, options);
}

export function updateSize(id, size, options) {
  return jsonRequest(`/sizes/${id}`, 'PUT', size, options);
}

export function deleteSize(id, options) {
  return deleteRequest(`/sizes/${id}`, options);
}

export function getColours(options) {
  return apiRequest('/colours', {
    ...options,
    method: 'GET',
  });
}

export function createColour(colour, options) {
  return jsonRequest('/colours', 'POST', colour, options);
}

export function updateColour(id, colour, options) {
  return jsonRequest(`/colours/${id}`, 'PUT', colour, options);
}

export function deleteColour(id, options) {
  return deleteRequest(`/colours/${id}`, options);
}

export function getProductVariants(productId, options) {
  return apiRequest(`/products/${productId}/variants`, {
    ...options,
    method: 'GET',
  });
}

export function getVariant(id, options) {
  return apiRequest(`/variants/${id}`, {
    ...options,
    method: 'GET',
  });
}

export function createVariant(productId, variant, options) {
  return jsonRequest(`/products/${productId}/variants`, 'POST', variant, options);
}

export function updateVariant(id, variant, options) {
  return jsonRequest(`/variants/${id}`, 'PUT', variant, options);
}

export function deleteVariant(id, options) {
  return deleteRequest(`/variants/${id}`, options);
}

export function getInventory(query, options) {
  const request = splitQueryAndOptions(query, options);

  return apiRequest('/inventory', {
    ...request.options,
    method: 'GET',
    query: request.query,
  });
}

export function getInventoryItem(variantId, options) {
  return apiRequest(`/inventory/${variantId}`, {
    ...options,
    method: 'GET',
  });
}

export function adjustStock(variantId, adjustment, options) {
  return jsonRequest(`/inventory/${variantId}/adjust`, 'POST', adjustment, options);
}

export function getStockHistory(variantId, options) {
  return apiRequest(`/inventory/${variantId}/history`, {
    ...options,
    method: 'GET',
  });
}

export function getLowStock(query, options) {
  const request = splitQueryAndOptions(query, options);

  return apiRequest('/inventory/low-stock', {
    ...request.options,
    method: 'GET',
    query: request.query,
  });
}

export function createInventoryWorkflow(request, options) {
  return jsonRequest('/inventory/agent/workflows', 'POST', request, options);
}

export function listInventoryWorkflows(query, options) {
  return apiRequest('/inventory/agent/workflows', {
    ...options,
    method: 'GET',
    query,
  });
}

export function getInventoryWorkflow(id, options) {
  return apiRequest(`/inventory/agent/workflows/${id}`, {
    ...options,
    method: 'GET',
  });
}

export function approveInventoryWorkflow(id, request = {}, options) {
  return jsonRequest(
    `/inventory/agent/workflows/${id}/approve`,
    'POST',
    request,
    options,
  );
}

export function rejectInventoryWorkflow(id, request = {}, options) {
  return jsonRequest(
    `/inventory/agent/workflows/${id}/reject`,
    'POST',
    request,
    options,
  );
}

export function reviseInventoryWorkflow(id, request, options) {
  return jsonRequest(
    `/inventory/agent/workflows/${id}/revise`,
    'POST',
    request,
    options,
  );
}
