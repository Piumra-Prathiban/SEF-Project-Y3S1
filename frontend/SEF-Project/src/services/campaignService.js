import { apiRequest } from './api';
import { toQueryString } from '../utils/queryString';

export function getCampaigns(token, query = {}) {
  return apiRequest(`/campaigns${toQueryString(query)}`, { token });
}

export function getCampaign(token, id) {
  return apiRequest(`/campaigns/${id}`, { token });
}

export function createCampaign(token, campaign) {
  return apiRequest('/campaigns', {
    method: 'POST',
    token,
    body: JSON.stringify(campaign),
  });
}

export function updateCampaign(token, id, campaign) {
  return apiRequest(`/campaigns/${id}`, {
    method: 'PUT',
    token,
    body: JSON.stringify(campaign),
  });
}

export function deleteCampaign(token, id) {
  return apiRequest(`/campaigns/${id}`, {
    method: 'DELETE',
    token,
  });
}
