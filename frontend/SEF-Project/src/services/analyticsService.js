import { apiRequest } from './api';
import { toQueryString } from '../utils/queryString';

// All figures are calculated by the ASP.NET Core API (/api/analytics).

export function getSalesSummary(token, query = {}) {
  return apiRequest(`/analytics/sales/summary${toQueryString(query)}`, { token });
}

export function getSalesOverTime(token, query = {}) {
  return apiRequest(`/analytics/sales/over-time${toQueryString(query)}`, { token });
}

export function getProductPerformance(token, query = {}) {
  return apiRequest(`/analytics/products/performance${toQueryString(query)}`, { token });
}

export function getInventoryStock(token, query = {}) {
  return apiRequest(`/analytics/inventory/stock${toQueryString(query)}`, { token });
}

export function getInventorySummary(token, query = {}) {
  return apiRequest(`/analytics/inventory/summary${toQueryString(query)}`, { token });
}

export function getPromotionPerformance(token, query = {}) {
  return apiRequest(`/analytics/promotions/performance${toQueryString(query)}`, { token });
}

export function getDemandInsights(token, query = {}) {
  return apiRequest(`/analytics/demand${toQueryString(query)}`, { token });
}
