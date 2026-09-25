// Converts an ASP.NET Core ValidationProblemDetails "errors" object
// ({ "DiscountValue": ["..."], "$.type": ["..."] }) into
// { discountValue: "...", type: "..." } keyed by camelCase field name.
export function toFieldErrors(error) {
  const errors = error?.data?.errors;

  if (!errors || typeof errors !== 'object') {
    return {};
  }

  return Object.entries(errors).reduce((result, [key, messages]) => {
    const name = key.replace(/^\$\.?/, '').split(/[.[]/)[0];
    const field = name
      ? name.charAt(0).toLowerCase() + name.slice(1)
      : 'form';

    if (!result[field]) {
      result[field] = Array.isArray(messages) ? messages[0] : String(messages);
    }

    return result;
  }, {});
}

// A human-readable message for any API error.
export function toErrorMessage(error) {
  if (!error) {
    return '';
  }

  if (error.status === 401) {
    return 'Your session has expired. Please log in again.';
  }

  if (error.status === 403) {
    return 'You do not have permission to do this.';
  }

  if (error.status === 404) {
    return 'The requested item was not found.';
  }

  if (error.data?.errors) {
    return 'Please correct the highlighted fields.';
  }

  return error.message || 'Something went wrong. Please try again.';
}
