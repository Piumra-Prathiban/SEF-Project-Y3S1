import { apiRequest } from './api';
import { toQueryString } from '../utils/queryString';

export function getPromotions(token, query = {}) {
  return apiRequest(`/promotions${toQueryString(query)}`, { token });
}

export function getPromotion(token, id) {
  return apiRequest(`/promotions/${id}`, { token });
}

export function getPromotionTargets(token) {
  return apiRequest('/promotions/targets', { token });
}

export function createPromotion(token, promotion) {
  return apiRequest('/promotions', {
    method: 'POST',
    token,
    body: JSON.stringify(promotion),
  });
}

export function updatePromotion(token, id, promotion) {
  return apiRequest(`/promotions/${id}`, {
    method: 'PUT',
    token,
    body: JSON.stringify(promotion),
  });
}

export function deletePromotion(token, id) {
  return apiRequest(`/promotions/${id}`, {
    method: 'DELETE',
    token,
  });
}

export function getPromotionPerformance(token, query = {}) {
  return apiRequest(
    `/analytics/promotions/performance${toQueryString(query)}`,
    { token }
  );
}
