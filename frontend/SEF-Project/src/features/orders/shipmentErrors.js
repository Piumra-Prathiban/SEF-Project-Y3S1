// Maps failures from the shipment endpoints to user-facing messages. The backend
// owns the shipment rules (allowed transitions, "order must be ready", one
// shipment per order), so its ProblemDetails message is surfaced for
// validation/conflict responses.
export function describeShipmentError(error) {
  if (error?.status === 403) {
    return 'You do not have permission to manage shipments.';
  }

  if (error?.status === 404) {
    return 'This order or shipment could not be found.';
  }

  return error?.message || 'The shipment action could not be completed.';
}
