// Pure helpers for the Purchase Orders (procurement) feature. These keep the
// form and page components thin and are unit-testable without touching the API.

export const PurchaseOrderStatus = Object.freeze({
  Draft: 0,
  Submitted: 1,
  Received: 2,
  Cancelled: 3,
});

export const PurchaseOrderStatusName = Object.freeze({
  0: 'Draft',
  1: 'Submitted',
  2: 'Received',
  3: 'Cancelled',
});

/** Status names in lifecycle order, for filter dropdowns. */
export const PURCHASE_ORDER_STATUS_OPTIONS = Object.freeze([
  'Draft',
  'Submitted',
  'Received',
  'Cancelled',
]);

/**
 * A blank purchase-order line for the create form.
 * @returns {{productVariantId: string, quantity: string, unitCost: string}}
 */
export function emptyPurchaseOrderItem() {
  return {
    productVariantId: '',
    quantity: '1',
    unitCost: '',
  };
}

function toNumber(value) {
  if (value === '' || value === null || value === undefined) {
    return null;
  }

  const parsed = Number(value);

  return Number.isFinite(parsed) ? parsed : null;
}

function roundCurrency(value) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

/**
 * Returns the readable status label for a purchase-order status value.
 * @param {number} status
 * @returns {string}
 */
export function getPurchaseOrderStatusName(status) {
  return PurchaseOrderStatusName[status] ?? 'Unknown';
}

/**
 * A draft purchase order can be submitted.
 * @param {number} status
 */
export function canSubmitPurchaseOrder(status) {
  return status === PurchaseOrderStatus.Draft;
}

/**
 * A submitted purchase order can be received.
 * @param {number} status
 */
export function canReceivePurchaseOrder(status) {
  return status === PurchaseOrderStatus.Submitted;
}

/**
 * A draft or submitted purchase order can be cancelled.
 * @param {number} status
 */
export function canCancelPurchaseOrder(status) {
  return status === PurchaseOrderStatus.Draft
    || status === PurchaseOrderStatus.Submitted;
}

/**
 * Line total for a single purchase-order line (quantity * unit cost).
 * @param {{quantity: number|string, unitCost: number|string}} item
 * @returns {number}
 */
export function calculateLineTotal(item) {
  const quantity = toNumber(item?.quantity);
  const unitCost = toNumber(item?.unitCost);

  if (quantity === null || unitCost === null) {
    return 0;
  }

  return roundCurrency(quantity * unitCost);
}

/**
 * Grand total across every purchase-order line.
 * @param {Array<{quantity: number|string, unitCost: number|string}>} items
 * @returns {number}
 */
export function calculatePurchaseOrderTotal(items = []) {
  const total = items.reduce(
    (sum, item) => sum + calculateLineTotal(item),
    0,
  );

  return roundCurrency(total);
}

/**
 * Validates the create-purchase-order form and returns a list of messages.
 * @param {{supplierId: string, items: Array}} form
 * @returns {string[]}
 */
export function validatePurchaseOrderForm(form) {
  const errors = [];

  if (!form?.supplierId) {
    errors.push('Select a supplier for this purchase order.');
  }

  const items = form?.items ?? [];

  if (items.length === 0) {
    errors.push('Add at least one product variant to the purchase order.');
  }

  items.forEach((item, index) => {
    const label = `Line ${index + 1}`;
    const quantity = toNumber(item.quantity);
    const unitCost = toNumber(item.unitCost);

    if (!item.productVariantId) {
      errors.push(`${label}: select a product variant.`);
    }

    if (quantity === null || !Number.isInteger(quantity) || quantity < 1) {
      errors.push(`${label}: quantity must be a whole number of 1 or more.`);
    }

    if (unitCost === null || unitCost < 0) {
      errors.push(`${label}: unit cost cannot be negative.`);
    }
  });

  return errors;
}

/**
 * Builds the API payload from the create form state.
 * @param {{supplierId: string, expectedAt: string, notes: string, items: Array}} form
 * @returns {import('./purchaseOrderService').PurchaseOrderCreateRequest}
 */
export function buildPurchaseOrderPayload(form) {
  return {
    supplierId: form.supplierId,
    expectedAt: form.expectedAt
      ? new Date(form.expectedAt).toISOString()
      : null,
    notes: form.notes?.trim() || null,
    items: (form.items ?? []).map((item) => ({
      productVariantId: item.productVariantId,
      quantity: Number(item.quantity),
      unitCost: Number(item.unitCost),
    })),
  };
}

/**
 * Turns an API error into a friendly, on-brand message for a given action.
 * @param {Error & {status?: number, isValidationError?: boolean}} error
 * @param {'create'|'submit'|'receive'|'cancel'} action
 * @returns {string}
 */
export function describePurchaseOrderError(error, action = 'update') {
  if (error?.status === 403) {
    return `You do not have permission to ${action} purchase orders.`;
  }

  if (error?.status === 404) {
    return 'This purchase order could not be found.';
  }

  if (error?.status === 409) {
    return error?.message
      || 'This purchase order cannot be changed in its current state.';
  }

  if (error?.isValidationError) {
    return error?.message || 'Please check the purchase order details.';
  }

  return error?.message || 'Something went wrong. Please try again.';
}
