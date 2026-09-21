const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5193/api';

export async function apiRequest(endpoint, options = {}) {
  const { token, ...fetchOptions } = options;

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...fetchOptions,
    headers: {
      'Content-Type': 'application/json',
      ...(token
        ? { Authorization: `Bearer ${token}` }
        : {}),
      ...fetchOptions.headers,
    },
  });

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    const error = new Error(
      data?.detail || data?.title || 'An API request failed.'
    );

    error.status = response.status;
    error.data = data;

    throw error;
  }

  return data;
}