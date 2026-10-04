import { apiRequest } from './api';
import { toQueryString } from '../utils/queryString';

const BASE = '/agents/fulfilment-exception/workflows';

export function getFulfilmentAgentWorkflows(token, query = {}) {
  return apiRequest(`${BASE}${toQueryString(query)}`, { token });
}

export function getFulfilmentAgentWorkflow(token, id) {
  return apiRequest(`${BASE}/${id}`, { token });
}

export function startFulfilmentAgentWorkflow(token, request) {
  return apiRequest(BASE, {
    method: 'POST',
    token,
    body: JSON.stringify(request),
  });
}

export function approveFulfilmentAgentWorkflow(token, id, comment) {
  return apiRequest(`${BASE}/${id}/approve`, {
    method: 'POST',
    token,
    body: JSON.stringify({ comment: comment || null }),
  });
}

export function rejectFulfilmentAgentWorkflow(token, id, comment) {
  return apiRequest(`${BASE}/${id}/reject`, {
    method: 'POST',
    token,
    body: JSON.stringify({ comment: comment || null }),
  });
}

export function reviseFulfilmentAgentWorkflow(token, id, comment) {
  return apiRequest(`${BASE}/${id}/revise`, {
    method: 'POST',
    token,
    body: JSON.stringify({ comment }),
  });
}
