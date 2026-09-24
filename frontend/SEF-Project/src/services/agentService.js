import { apiRequest } from './api';
import { toQueryString } from '../utils/queryString';

const BASE = '/agents/inventory-promotion/workflows';

export function getAgentWorkflows(token, query = {}) {
  return apiRequest(`${BASE}${toQueryString(query)}`, { token });
}

export function getAgentWorkflow(token, id) {
  return apiRequest(`${BASE}/${id}`, { token });
}

export function startAgentWorkflow(token, request) {
  return apiRequest(BASE, {
    method: 'POST',
    token,
    body: JSON.stringify(request),
  });
}

export function approveAgentWorkflow(token, id, comment) {
  return apiRequest(`${BASE}/${id}/approve`, {
    method: 'POST',
    token,
    body: JSON.stringify({ comment: comment || null }),
  });
}

export function rejectAgentWorkflow(token, id, comment) {
  return apiRequest(`${BASE}/${id}/reject`, {
    method: 'POST',
    token,
    body: JSON.stringify({ comment: comment || null }),
  });
}

export function reviseAgentWorkflow(token, id, request) {
  return apiRequest(`${BASE}/${id}/revise`, {
    method: 'POST',
    token,
    body: JSON.stringify(request),
  });
}
