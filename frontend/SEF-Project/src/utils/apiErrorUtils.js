function flattenValidationErrors(errors) {
  if (!errors) {
    return null;
  }

  if (Array.isArray(errors)) {
    return errors.join(' ');
  }

  if (typeof errors === 'object') {
    return Object.values(errors).flat().join(' ');
  }

  return String(errors);
}

function hasInternalDetails(message) {
  return /exception|stack trace|\bat\b .*:\d+|system\./i.test(message);
}

export function normalizeApiError(error) {
  const validationMessage = flattenValidationErrors(error?.errors);

  if (validationMessage) {
    return validationMessage;
  }

  if (error?.isUnauthorized || error?.status === 401) {
    return 'Your session has expired or you are not signed in. Please sign in again.';
  }

  if (error?.isForbidden || error?.status === 403) {
    return 'You do not have permission to perform this action.';
  }

  if (error?.isNotFound || error?.status === 404) {
    return 'The requested record could not be found.';
  }

  if (error?.isConflict || error?.status === 409) {
    return error.detail || error.title || 'This action conflicts with existing data.';
  }

  if (error?.isValidationError || error?.status === 400 || error?.status === 422) {
    return error.detail || error.title || 'Please check the form and try again.';
  }

  if (error?.isServerError || error?.status >= 500) {
    return 'The server could not complete the request. Please try again later.';
  }

  const message = error?.detail || error?.message || 'Something went wrong.';

  if (hasInternalDetails(message)) {
    return 'The request could not be completed. Please try again.';
  }

  return message;
}
