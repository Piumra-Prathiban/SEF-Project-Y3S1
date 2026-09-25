import { apiRequest } from '../../services/api.js';

/**
 * @typedef {Object} ProfileDto
 * @property {number} customerId
 * @property {string} email
 * @property {string} firstName
 * @property {string} lastName
 * @property {string} memberSince
 *
 * @typedef {Object} AddressDto
 * @property {number} id
 * @property {string} label
 * @property {string} addressLine1
 * @property {string|null} addressLine2
 * @property {string} city
 * @property {string|null} province
 * @property {string} postalCode
 * @property {string} country
 * @property {boolean} isDefault
 *
 * @typedef {Object} AddressRequestDto
 * @property {string} label
 * @property {string} addressLine1
 * @property {string} addressLine2
 * @property {string} city
 * @property {string} province
 * @property {string} postalCode
 * @property {string} country
 * @property {boolean} isDefault
 */

/** @returns {Promise<ProfileDto>} */
export function getProfile(token) {
  return apiRequest('/profile', { token });
}

/**
 * @param {string} token
 * @param {{email: string, firstName: string, lastName: string}} profile
 * @returns {Promise<ProfileDto>}
 */
export function updateProfile(token, profile) {
  return apiRequest('/profile', {
    method: 'PUT',
    token,
    body: JSON.stringify(profile),
  });
}

/** @returns {Promise<AddressDto[]>} */
export function getAddresses(token) {
  return apiRequest('/profile/addresses', { token });
}

/**
 * @param {string} token
 * @param {AddressRequestDto} address
 * @returns {Promise<AddressDto>}
 */
export function createAddress(token, address) {
  return apiRequest('/profile/addresses', {
    method: 'POST',
    token,
    body: JSON.stringify(address),
  });
}

/**
 * @param {string} token
 * @param {number} addressId
 * @param {AddressRequestDto} address
 * @returns {Promise<AddressDto>}
 */
export function updateAddress(token, addressId, address) {
  return apiRequest(`/profile/addresses/${addressId}`, {
    method: 'PUT',
    token,
    body: JSON.stringify(address),
  });
}

/**
 * @param {string} token
 * @param {number} addressId
 * @returns {Promise<void>}
 */
export function deleteAddress(token, addressId) {
  return apiRequest(`/profile/addresses/${addressId}`, {
    method: 'DELETE',
    token,
  });
}
