import { apiRequest } from './api';

export async function login(email, password) {
  return apiRequest('/Auth/login', {
    method: 'POST',
    body: JSON.stringify({
      email,
      password,
    }),
  });
}

export async function register(userData) {
  return apiRequest('/Auth/register', {
    method: 'POST',
    body: JSON.stringify(userData),
  });
}
