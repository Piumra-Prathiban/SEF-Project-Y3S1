// Builds "?a=1&b=2" from an object, skipping empty values.
export function toQueryString(params = {}) {
  const search = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    search.append(key, String(value));
  });

  const text = search.toString();
  return text ? `?${text}` : '';
}
