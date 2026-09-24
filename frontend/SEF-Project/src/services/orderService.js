import { apiRequest } from './api';

// Wire contracts for the Orders & Fulfilment API. The shapes below mirror the
// backend DTOs in DTOs/Orders/*.cs one-to-one: the API serializes C# enums as
// their underlying integers, dates as ISO 8601 strings, and (via ASP.NET Core
// web defaults) all property names as camelCase.

export const OrderStatus = Object.freeze({
  Pending: 0,
  Confirmed: 1,
  Preparing: 2,
  Ready: 3,
  Completed: 4,
  Cancelled: 5,
  Refunded: 6,
});

export const PaymentStatus = Object.freeze({
  Pending: 0,
  Completed: 1,
  Failed: 2,
  Refunded: 3,
});

export const PaymentMethod = Object.freeze({
  Card: 0,
  Cash: 1,
  OnlineTransfer: 2,
});

export const ShipmentStatus = Object.freeze({
  Pending: 0,
  Shipped: 1,
  Delivered: 2,
  Cancelled: 3,
});

export const OrderStatusName = Object.freeze({
  0: 'Pending',
  1: 'Confirmed',
  2: 'Preparing',
  3: 'Ready',
  4: 'Completed',
  5: 'Cancelled',
  6: 'Refunded',
});

export const PaymentStatusName = Object.freeze({
  0: 'Pending',
  1: 'Completed',
  2: 'Failed',
  3: 'Refunded',
});

export const PaymentMethodName = Object.freeze({
  0: 'Card',
  1: 'Cash',
  2: 'OnlineTransfer',
});

export const ShipmentStatusName = Object.freeze({
  0: 'Pending',
  1: 'Shipped',
  2: 'Delivered',
  3: 'Cancelled',
});

/**
 * @typedef {Object} OrderItemResponse
 * @property {string} id
 * @property {string} productVariantId
 * @property {string} sku
 * @property {string} name
 * @property {number} quantity
 * @property {number} unitPrice
 * @property {number} lineTotal
 */

/**
 * @typedef {Object} OrderAddressResponse
 * @property {string} fullName
 * @property {string} line1
 * @property {string|null} line2
 * @property {string} city
 * @property {string|null} province
 * @property {string} postalCode
 * @property {string} country
 * @property {string|null} phone
 */

/**
 * @typedef {Object} PaymentResponse
 * @property {string} id
 * @property {number} amount
 * @property {number} method PaymentMethod value
 * @property {number} status PaymentStatus value
 * @property {string|null} transactionReference
 * @property {string|null} paidAt ISO 8601
 */

/**
 * @typedef {Object} ShipmentResponse
 * @property {string} id
 * @property {number} status ShipmentStatus value
 * @property {string|null} trackingNumber
 * @property {string|null} carrier
 * @property {string|null} shippedAt ISO 8601
 * @property {string|null} deliveredAt ISO 8601
 */

/**
 * @typedef {Object} OrderStatusHistoryResponse
 * @property {string} id
 * @property {number} status OrderStatus value
 * @property {string} changedAt ISO 8601
 * @property {string|null} note
 */

/**
 * @typedef {Object} OrderResponse
 * @property {string} id
 * @property {string} orderNumber
 * @property {number} status OrderStatus value
 * @property {string} placedAt ISO 8601
 * @property {number} subtotal
 * @property {number} discountTotal
 * @property {number} taxAmount
 * @property {number} shippingFee
 * @property {number} total
 * @property {string} currency
 * @property {OrderItemResponse[]} items
 * @property {OrderAddressResponse|null} deliveryAddress
 * @property {PaymentResponse[]} payments
 * @property {ShipmentResponse[]} shipments
 * @property {OrderStatusHistoryResponse[]} statusHistory
 */

/**
 * @typedef {Object} OrderSummaryResponse
 * @property {string} id
 * @property {string} orderNumber
 * @property {number} status OrderStatus value
 * @property {string} placedAt ISO 8601
 * @property {number} total
 * @property {string} currency
 */

/**
 * @typedef {Object} OrderListResponse
 * @property {OrderSummaryResponse[]} items
 * @property {number} totalCount
 * @property {number} page
 * @property {number} pageSize
 */

/**
 * @typedef {Object} CreateOrderItemRequest
 * @property {string} productVariantId
 * @property {number} quantity positive integer
 */

/**
 * @typedef {Object} CreateOrderAddressRequest
 * @property {string} fullName
 * @property {string} line1
 * @property {string} [line2]
 * @property {string} city
 * @property {string} [province]
 * @property {string} postalCode
 * @property {string} [country]
 * @property {string} [phone]
 */

/**
 * @typedef {Object} CreateOrderRequest
 * @property {CreateOrderItemRequest[]} items
 * @property {CreateOrderAddressRequest} deliveryAddress
 * @property {number} paymentMethod PaymentMethod value
 * @property {string} [couponCode]
 */

/**
 * @typedef {Object} UpdateOrderStatusRequest
 * @property {number} status OrderStatus value
 * @property {string} [note]
 */

/**
 * @typedef {Object} CreatePaymentRequest
 * @property {number} method PaymentMethod value
 * @property {number} amount
 */

/**
 * @typedef {Object} UpdatePaymentStatusRequest
 * @property {number} status PaymentStatus value
 * @property {string} [transactionReference]
 */

/**
 * @typedef {Object} CreateShipmentRequest
 * @property {string} [carrier]
 * @property {string} [trackingNumber]
 */

/**
 * @typedef {Object} UpdateShipmentRequest
 * @property {number} status ShipmentStatus value
 * @property {string} [carrier]
 * @property {string} [trackingNumber]
 */

/**
 * @typedef {Object} CancelOrderRequest
 * @property {string} [reason]
 */

/**
 * @typedef {Object} OrderQuery Parameters for GET /Orders.
 * @property {number} [page] 1-based page number (backend default 1).
 * @property {number} [pageSize] Page size 1-100 (backend default 20).
 * @property {string} [sortBy] One of 'total', 'orderNumber', 'status',
 * 'createdAt'; the backend falls back to placedAt for anything else.
 * @property {string} [sortDirection] 'asc' for ascending; the backend treats
 * anything else as descending.
 * @property {number|string} [status] OrderStatus value or its name
 * (e.g. 'Pending'), matching the backend query binding.
 * @property {Date|string} [from] ISO 8601 lower bound on placedAt.
 * @property {Date|string} [to] ISO 8601 upper bound on placedAt.
 * @property {string} [orderNumber]
 * @property {number} [customerId] Staff-only filter; ignored for customers.
 */

function toIsoDate(value) {
  return value instanceof Date ? value.toISOString() : String(value);
}

/**
 * Builds the query string for GET /Orders, skipping undefined/empty values.
 *
 * @param {OrderQuery} [query]
 * @returns {string} '' or a leading-'?' query string.
 */
export function buildOrderQuery(query = {}) {
  const params = new URLSearchParams();

  if (query.page !== undefined && query.page !== null) {
    params.set('page', query.page);
  }
  if (query.pageSize !== undefined && query.pageSize !== null) {
    params.set('pageSize', query.pageSize);
  }
  if (query.sortBy) {
    params.set('sortBy', query.sortBy);
  }
  if (query.sortDirection) {
    params.set('sortDirection', query.sortDirection);
  }
  if (query.status !== undefined && query.status !== null) {
    params.set('status', query.status);
  }
  if (query.from) {
    params.set('from', toIsoDate(query.from));
  }
  if (query.to) {
    params.set('to', toIsoDate(query.to));
  }
  if (query.orderNumber) {
    params.set('orderNumber', query.orderNumber);
  }
  if (query.customerId !== undefined && query.customerId !== null) {
    params.set('customerId', query.customerId);
  }

  const queryString = params.toString();

  return queryString ? `?${queryString}` : '';
}

/**
 * Creates an order (checkout).
 * @param {string} token
 * @param {CreateOrderRequest} payload
 * @returns {Promise<OrderResponse>}
 */
export async function createOrder(token, payload) {
  return apiRequest('/Orders', {
    method: 'POST',
    token,
    body: JSON.stringify(payload),
  });
}

/**
 * Lists orders (paged/sortable/filterable).
 * @param {string} token
 * @param {OrderQuery} [query]
 * @returns {Promise<OrderListResponse>}
 */
export async function getOrders(token, query = {}) {
  return apiRequest(`/Orders${buildOrderQuery(query)}`, {
    method: 'GET',
    token,
  });
}

/**
 * Gets a single order including items, address, payments, shipments and
 * status history.
 * @param {string} token
 * @param {string} id Order Guid.
 * @returns {Promise<OrderResponse>}
 */
export async function getOrderById(token, id) {
  return apiRequest(`/Orders/${id}`, {
    method: 'GET',
    token,
  });
}

/**
 * Gets the status history of an order.
 * @param {string} token
 * @param {string} id Order Guid.
 * @returns {Promise<OrderStatusHistoryResponse[]>}
 */
export async function getOrderStatusHistory(token, id) {
  return apiRequest(`/Orders/${id}/status-history`, {
    method: 'GET',
    token,
  });
}

/**
 * Updates an order's status (Staff/Administrator only).
 * @param {string} token
 * @param {string} id Order Guid.
 * @param {UpdateOrderStatusRequest} payload
 * @returns {Promise<OrderResponse>}
 */
export async function updateOrderStatus(token, id, payload) {
  return apiRequest(`/Orders/${id}/status`, {
    method: 'PATCH',
    token,
    body: JSON.stringify(payload),
  });
}

/**
 * Records a payment against an order.
 * @param {string} token
 * @param {string} id Order Guid.
 * @param {CreatePaymentRequest} payload
 * @returns {Promise<PaymentResponse>}
 */
export async function createPayment(token, id, payload) {
  return apiRequest(`/Orders/${id}/payments`, {
    method: 'POST',
    token,
    body: JSON.stringify(payload),
  });
}

/**
 * Lists payments for an order.
 * @param {string} token
 * @param {string} id Order Guid.
 * @returns {Promise<PaymentResponse[]>}
 */
export async function getPayments(token, id) {
  return apiRequest(`/Orders/${id}/payments`, {
    method: 'GET',
    token,
  });
}

/**
 * Updates a payment's status (Staff/Administrator only).
 * @param {string} token
 * @param {string} id Order Guid.
 * @param {string} paymentId Payment Guid.
 * @param {UpdatePaymentStatusRequest} payload
 * @returns {Promise<PaymentResponse>}
 */
export async function updatePaymentStatus(token, id, paymentId, payload) {
  return apiRequest(`/Orders/${id}/payments/${paymentId}/status`, {
    method: 'PATCH',
    token,
    body: JSON.stringify(payload),
  });
}

/**
 * Creates a shipment for an order (Staff/Administrator only).
 * @param {string} token
 * @param {string} id Order Guid.
 * @param {CreateShipmentRequest} payload
 * @returns {Promise<ShipmentResponse>}
 */
export async function createShipment(token, id, payload) {
  return apiRequest(`/Orders/${id}/shipments`, {
    method: 'POST',
    token,
    body: JSON.stringify(payload),
  });
}

/**
 * Lists shipments for an order.
 * @param {string} token
 * @param {string} id Order Guid.
 * @returns {Promise<ShipmentResponse[]>}
 */
export async function getShipments(token, id) {
  return apiRequest(`/Orders/${id}/shipments`, {
    method: 'GET',
    token,
  });
}

/**
 * Updates a shipment's status (Staff/Administrator only).
 * @param {string} token
 * @param {string} id Order Guid.
 * @param {string} shipmentId Shipment Guid.
 * @param {UpdateShipmentRequest} payload
 * @returns {Promise<ShipmentResponse>}
 */
export async function updateShipmentStatus(token, id, shipmentId, payload) {
  return apiRequest(`/Orders/${id}/shipments/${shipmentId}/status`, {
    method: 'PATCH',
    token,
    body: JSON.stringify(payload),
  });
}

/**
 * Cancels an order (customer cancels own order; staff any).
 * @param {string} token
 * @param {string} id Order Guid.
 * @param {CancelOrderRequest} [payload]
 * @returns {Promise<OrderResponse>}
 */
export async function cancelOrder(token, id, payload = {}) {
  return apiRequest(`/Orders/${id}/cancel`, {
    method: 'POST',
    token,
    body: JSON.stringify(payload),
  });
}
