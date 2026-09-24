// Maps failures from the payment endpoints to user-facing messages. The backend
// keeps the business rules (outstanding balance, allowed transitions), so its
// ProblemDetails message is surfaced for validation/conflict responses.
export function describePaymentError(error) {
  if (error?.status === 403) {
    return 'You do not have permission to perform this payment action.';
  }

  if (error?.status === 404) {
    return 'This order or payment could not be found.';
  }

  return error?.message || 'The payment action could not be completed.';
}
