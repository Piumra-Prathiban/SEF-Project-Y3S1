// Product imagery is seeded as an origin-relative path (for example
// "/images/products/classic-cotton-tshirt.svg") so the seed stays
// host-independent. The browser still needs an absolute URL, so relative
// paths are resolved against the API origin while absolute URLs pass through.
const API_BASE_URL =
  import.meta.env?.VITE_API_BASE_URL || 'http://localhost:5193/api';

// The configured base URL ends in "/api"; static assets live on the origin.
const API_ORIGIN = API_BASE_URL.replace(/\/api\/?$/, '');

const ABSOLUTE_URL_PATTERN = /^(?:[a-z][a-z\d+.-]*:|\/\/)/i;

export function resolveImageUrl(url) {
  if (!url) {
    return null;
  }

  if (ABSOLUTE_URL_PATTERN.test(url)) {
    return url;
  }

  return `${API_ORIGIN}${url.startsWith('/') ? '' : '/'}${url}`;
}
