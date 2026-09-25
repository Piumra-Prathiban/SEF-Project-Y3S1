import { apiRequest } from '../../services/api';

// Wire contracts for the Purchase Orders (procurement) API. The shapes mirror
// the backend DTOs in DTOs/Catalog/PurchaseOrderDtos.cs one-to-one: the API
// serializes C# enums as their underlying integers, dates as ISO 8601 strings
// and (via ASP.NET Core web defaults) all property names as camelCase.

/**
 * @typedef {Object} PurchaseOrderItemResponse
 * @property {string} id
 * @property {string} productVariantId
 * @property {string} sku
 * @property {string} productName
 * @property {string} variantName
 * @property {number} quantity
 * @property {number} unitCost
 * @property {number} lineTotal
 */

/**
 * @typedef {Object} PurchaseOrderResponse
 * @property {string} id
 * @property {string} orderNumber
 * @property {string} supplierId
 * @property {string} supplierName
 * @property {number} status PurchaseOrderStatus value
 * @property {string|null} expectedAt ISO 8601
 * @property {string|null} submittedAt ISO 8601
 * @property {string|null} receivedAt ISO 8601
 * @property {string|null} notes
 * @property {PurchaseOrderItemResponse[]} items
 * @property {number} total
 * @property {string} createdAt ISO 8601
 * @property {string} updatedAt ISO 8601
 */

/**
 * @typedef {Object} PurchaseOrderItemRequest
 * @property {string} productVariantId
 * @property {number} quantity positive integer
 * @property {number} unitCost zero or greater
 */

/**
 * @typedef {Object} PurchaseOrderCreateRequest
 * @property {string} supplierId
 * @property {string|null} [expectedAt] ISO 8601
 * @property {string|null} [notes]
 * @property {PurchaseOrderItemRequest[]} items
 */

/**
 * @typedef {Object} PurchaseOrderQuery Parameters for GET /PurchaseOrders.
 * @property {number} [page] 1-based page number (backend default 1).
 * @property {number} [pageSize] Page size 1-100 (backend default 20).
 * @property {string} [supplierId]
 * @property {number|string} [status] PurchaseOrderStatus value or its name
 * (e.g. 'Draft'), matching the backend query binding.
 */

function toQueryValue(value) {
  return value instanceof Date ? value.toISOString() : String(value);
}

/**
 * Builds the query string for GET /PurchaseOrders, skipping undefined/empty
 * values.
 *
 * @param {PurchaseOrderQuery} [query]
 * @returns {string} '' or a leading-'?' query string.
 */
export function buildPurchaseOrderQuery(query = {}) {
  const params = new URLSearchParams();

  if (query.page !== undefined && query.page !== null) {
    params.set('page', query.page);
  }
  if (query.pageSize !== undefined && query.pageSize !== null) {
    params.set('pageSize', query.pageSize);
  }
  if (query.supplierId !== undefined && query.supplierId !== null && query.supplierId !== '') {
    params.set('supplierId', query.supplierId);
  }
  if (query.status !== undefined && query.status !== null && query.status !== '') {
    params.set('status', toQueryValue(query.status));
  }

  const queryString = params.toString();

  return queryString ? `?${queryString}` : '';
}

/**
 * Lists purchase orders (paged, filtered by supplier/status, newest first).
 * @param {string} token
 * @param {PurchaseOrderQuery} [query]
 * @returns {Promise<{items: PurchaseOrderResponse[], page: number, pageSize: number, totalItems: number, totalPages: number}>}
 */
export async function getPurchaseOrders(token, query = {}) {
  return apiRequest(`/PurchaseOrders${buildPurchaseOrderQuery(query)}`, {
    method: 'GET',
    token,
  });
}

/**
 * Gets a single purchase order with its lines and totals.
 * @param {string} token
 * @param {string} id Purchase order Guid.
 * @returns {Promise<PurchaseOrderResponse>}
 */
export async function getPurchaseOrderById(token, id) {
  return apiRequest(`/PurchaseOrders/${id}`, {
    method: 'GET',
    token,
  });
}

/**
 * Raises a new draft purchase order against a supplier (Staff/Administrator).
 * @param {string} token
 * @param {PurchaseOrderCreateRequest} payload
 * @returns {Promise<PurchaseOrderResponse>}
 */
export async function createPurchaseOrder(token, payload) {
  return apiRequest('/PurchaseOrders', {
    method: 'POST',
    token,
    body: JSON.stringify(payload),
  });
}

/**
 * Submits a draft purchase order to the supplier (Staff/Administrator).
 * @param {string} token
 * @param {string} id Purchase order Guid.
 * @returns {Promise<PurchaseOrderResponse>}
 */
export async function submitPurchaseOrder(token, id) {
  return apiRequest(`/PurchaseOrders/${id}/submit`, {
    method: 'POST',
    token,
  });
}

/**
 * Receives a submitted purchase order, increasing stock for every line
 * (Staff/Administrator).
 * @param {string} token
 * @param {string} id Purchase order Guid.
 * @returns {Promise<PurchaseOrderResponse>}
 */
export async function receivePurchaseOrder(token, id) {
  return apiRequest(`/PurchaseOrders/${id}/receive`, {
    method: 'POST',
    token,
  });
}

/**
 * Cancels a draft or submitted purchase order (Staff/Administrator).
 * @param {string} token
 * @param {string} id Purchase order Guid.
 * @returns {Promise<PurchaseOrderResponse>}
 */
export async function cancelPurchaseOrder(token, id) {
  return apiRequest(`/PurchaseOrders/${id}/cancel`, {
    method: 'POST',
    token,
  });
}

/**
 * Lists suppliers so staff can pick the supplier a purchase order is raised
 * against. Reuses the existing catalog endpoint.
 * @param {string} token
 * @returns {Promise<Array<{id: string, name: string, isActive: boolean}>>}
 */
export async function getSuppliers(token) {
  return apiRequest('/suppliers', {
    method: 'GET',
    token,
  });
}

/**
 * Flattens the catalog's products-with-variants payload into a flat list of
 * variant options (id, SKU, product name) for the purchase-order line picker.
 * @param {string} token
 * @returns {Promise<Array<{id: string, sku: string, productName: string, variantName: string}>>}
 */
export async function getVariantOptions(token) {
  const response = await apiRequest('/products', {
    method: 'GET',
    token,
    query: { pageSize: 100 },
  });

  const products = response?.items ?? [];

  return products.flatMap((product) =>
    (product.variants ?? []).map((variant) => ({
      id: variant.id,
      sku: variant.sku,
      productName: variant.productName || product.name,
      variantName: variant.name,
    })),
  );
}
