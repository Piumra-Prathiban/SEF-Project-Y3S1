const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL || 'http://localhost:5193/api';

export class ApiError extends Error {
  constructor(message, response, data) {
    super(message);
    this.name = 'ApiError';
    this.status = response.status;
    this.statusText = response.statusText;
    this.data = data;
    this.errors = data?.errors ?? null;
    this.title = data?.title ?? null;
    this.detail = data?.detail ?? null;
    this.type = data?.type ?? null;
    this.instance = data?.instance ?? null;
    this.isUnauthorized = response.status === 401;
    this.isForbidden = response.status === 403;
    this.isNotFound = response.status === 404;
    this.isConflict = response.status === 409;
    this.isValidationError = response.status === 400 || response.status === 422;
    this.isServerError = response.status >= 500;
  }
}

function buildUrl(endpoint, query) {
  const url = new URL(`${API_BASE_URL}${endpoint}`);

  if (!query) {
    return url.toString();
  }

  Object.entries(query).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    if (Array.isArray(value)) {
      value.forEach((item) => {
        if (item !== undefined && item !== null && item !== '') {
          url.searchParams.append(key, item);
        }
      });
      return;
    }

    url.searchParams.set(key, value);
  });

  return url.toString();
}

export async function apiRequest(endpoint, options = {}) {
  const { token, query, ...fetchOptions } = options;
  const hasBody = fetchOptions.body !== undefined && fetchOptions.body !== null;

  const response = await fetch(buildUrl(endpoint, query), {
    ...fetchOptions,
    headers: {
      ...(hasBody ? { 'Content-Type': 'application/json' } : {}),
      ...(token
        ? { Authorization: `Bearer ${token}` }
        : {}),
      ...fetchOptions.headers,
    },
  });

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    throw new ApiError(
      data?.detail || data?.title || 'An API request failed.',
      response,
      data,
    );
  }

  return data;
}
