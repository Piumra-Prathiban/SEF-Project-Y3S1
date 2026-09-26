function firstDefined(...values) {
  return values.find((value) => value !== undefined && value !== null);
}

export const defaultInventoryQuery = {
  product: '',
  sku: '',
  categoryId: '',
  stockStatus: '',
  lowStockOnly: false,
  page: 1,
  pageSize: 10,
};

export const defaultLowStockQuery = {
  sortBy: 'product',
  sortDirection: 'asc',
  page: 1,
  pageSize: 10,
};

export function updateInventoryQuery(currentQuery, field, value) {
  return {
    ...currentQuery,
    [field]: value,
    page: field === 'page' ? value : 1,
  };
}

export function updateLowStockQuery(currentQuery, field, value) {
  return {
    ...currentQuery,
    [field]: value,
    page: field === 'page' ? value : 1,
  };
}

export function normalizeInventoryItems(response) {
  if (Array.isArray(response)) {
    return response;
  }

  return response?.items ?? [];
}

export function normalizeStockHistoryItems(response) {
  if (Array.isArray(response)) {
    return response;
  }

  return response?.items ?? response?.transactions ?? [];
}

export function getVariantId(item) {
  return firstDefined(
    item.variantId,
    item.productVariantId,
    item.variant?.id,
    item.productVariant?.id,
    item.id,
  );
}

export function getInventoryQuantity(item) {
  return firstDefined(
    item.quantityOnHand,
    item.currentStock,
    item.quantity,
    item.stockQuantity,
    item.inventoryStock?.quantityOnHand,
    item.inventory?.quantityOnHand,
    item.stock?.quantityOnHand,
    0,
  );
}

export function getReorderLevel(item) {
  return firstDefined(
    item.reorderLevel,
    item.inventoryStock?.reorderLevel,
    item.inventory?.reorderLevel,
    item.stock?.reorderLevel,
    0,
  );
}

export function getShortageAmount(item) {
  const providedShortage = firstDefined(
    item.shortageAmount,
    item.shortageQuantity,
    item.shortfall,
    item.reorderShortage,
  );

  if (providedShortage !== undefined) {
    return providedShortage;
  }

  const quantity = Number(getInventoryQuantity(item));
  const reorderLevel = Number(getReorderLevel(item));

  if (Number.isNaN(quantity) || Number.isNaN(reorderLevel)) {
    return '-';
  }

  return Math.max(reorderLevel - quantity, 0);
}

export function getInventoryStatus(item) {
  const backendStatus = item.status ?? item.stockStatus ?? item.inventoryStatus;

  if (backendStatus) {
    return backendStatus;
  }

  const quantity = Number(getInventoryQuantity(item));
  const reorderLevel = Number(getReorderLevel(item));

  if (quantity <= 0) {
    return 'Out of Stock';
  }

  if (quantity <= reorderLevel) {
    return 'Low Stock';
  }

  return 'In Stock';
}

export function getProductName(item) {
  return firstDefined(
    item.productName,
    item.product?.name,
    item.variant?.productName,
    item.productVariant?.productName,
    item.productVariant?.product?.name,
    '-',
  );
}

export function getSku(item) {
  return firstDefined(
    item.sku,
    item.variant?.sku,
    item.productVariant?.sku,
    '-',
  );
}

export function getSizeName(item) {
  return firstDefined(
    item.sizeName,
    item.size?.name,
    item.variant?.sizeName,
    item.variant?.size?.name,
    item.productVariant?.sizeName,
    item.productVariant?.size?.name,
    '-',
  );
}

export function getColourName(item) {
  return firstDefined(
    item.colourName,
    item.colorName,
    item.colour?.name,
    item.color?.name,
    item.variant?.colourName,
    item.variant?.colorName,
    item.variant?.colour?.name,
    item.productVariant?.colourName,
    item.productVariant?.colour?.name,
    '-',
  );
}

export function filterInventoryItems(items, query) {
  const productSearch = query.product.trim().toLocaleLowerCase();
  const skuSearch = query.sku.trim().toLocaleLowerCase();

  return items.filter((item) => {
    const matchesProduct = !productSearch || getProductName(item).toLocaleLowerCase().includes(productSearch);
    const matchesSku = !skuSearch || getSku(item).toLocaleLowerCase().includes(skuSearch);
    const matchesCategory = !query.categoryId || String(item.categoryId) === String(query.categoryId);
    const matchesStatus = !query.stockStatus || getInventoryStatus(item) === query.stockStatus;
    const matchesLowStock = !query.lowStockOnly || item.isLowStock === true || Number(getInventoryQuantity(item)) <= Number(getReorderLevel(item));
    return matchesProduct && matchesSku && matchesCategory && matchesStatus && matchesLowStock;
  });
}

export function sortLowStockItems(items, query) {
  const fieldValue = (item) => {
    switch (query.sortBy) {
      case 'sku': return getSku(item);
      case 'quantity': return Number(getInventoryQuantity(item));
      case 'reorderLevel': return Number(getReorderLevel(item));
      case 'shortage': return Number(getShortageAmount(item));
      default: return getProductName(item);
    }
  };
  const direction = query.sortDirection === 'desc' ? -1 : 1;
  return [...items].sort((left, right) => {
    const first = fieldValue(left);
    const second = fieldValue(right);
    const result = typeof first === 'number' && typeof second === 'number'
      ? first - second
      : String(first).localeCompare(String(second));
    return (result || getSku(left).localeCompare(getSku(right))) * direction;
  });
}

export function paginateItems(items, requestedPage, requestedPageSize) {
  const pageSize = Math.max(1, Number(requestedPageSize) || 10);
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));
  const page = Math.min(Math.max(1, Number(requestedPage) || 1), totalPages);
  return {
    items: items.slice((page - 1) * pageSize, page * pageSize),
    page,
    pageSize,
    totalItems: items.length,
    totalPages,
  };
}

export function buildInventorySummary(inventoryItems, lowStockItems) {
  const productIds = new Set(
    inventoryItems
      .map((item) => firstDefined(
        item.productId,
        item.product?.id,
        item.variant?.productId,
        item.productVariant?.productId,
        item.productVariant?.product?.id,
      ))
      .filter(Boolean),
  );

  const totalStock = inventoryItems.reduce(
    (sum, item) => sum + Number(getInventoryQuantity(item) || 0),
    0,
  );

  const outOfStockVariants = inventoryItems.filter(
    (item) => Number(getInventoryQuantity(item)) <= 0,
  ).length;

  return {
    totalProducts: productIds.size,
    totalVariants: inventoryItems.length,
    totalStock,
    lowStockVariants: lowStockItems.length,
    outOfStockVariants,
  };
}

export const STOCK_TRANSACTION_TYPES = [
  { value: 'StockIn', apiValue: 1, label: 'Add stock' },
  { value: 'StockOut', apiValue: 2, label: 'Remove stock' },
  { value: 'Adjustment', apiValue: 0, label: 'Positive correction' },
];

const TRANSACTION_TYPE_NAMES = ['Adjustment', 'Stock in', 'Stock out', 'Receipt', 'Sale', 'Reservation', 'Reservation release', 'Transfer'];

export function validateStockAdjustment(adjustment) {
  const errors = [];
  const quantity = Number(adjustment.quantity);

  if (!adjustment.transactionType) {
    errors.push('Transaction type is required.');
  } else if (!STOCK_TRANSACTION_TYPES.some((type) => type.value === adjustment.transactionType)) {
    errors.push('Transaction type is not valid.');
  }

  if (adjustment.quantity === '' || Number.isNaN(quantity)) {
    errors.push('Quantity is required.');
  } else if (!Number.isInteger(quantity) || quantity <= 0) {
    errors.push('Quantity must be a whole number greater than zero.');
  }

  if (!adjustment.reason?.trim()) {
    errors.push('Reason is required.');
  }

  return errors;
}

export function buildStockAdjustmentPayload(adjustment) {
  return {
    type: STOCK_TRANSACTION_TYPES.find((item) => item.value === adjustment.transactionType).apiValue,
    quantity: Number(adjustment.quantity),
    reason: adjustment.reason.trim(),
  };
}

export function getHistoryDate(transaction) {
  return firstDefined(
    transaction.timestamp,
    transaction.createdAt,
    transaction.transactionDate,
    transaction.occurredAt,
    transaction.date,
    null,
  );
}

export function getHistoryType(transaction) {
  const type = firstDefined(
    transaction.transactionType,
    transaction.type,
    transaction.adjustmentType,
    '-',
  );
  return typeof type === 'number' ? TRANSACTION_TYPE_NAMES[type] ?? 'Unknown movement' : type;
}

export function getHistoryQuantity(transaction) {
  return firstDefined(transaction.quantity, transaction.adjustmentQuantity, '-');
}

export function getHistoryPreviousQuantity(transaction) {
  return firstDefined(
    transaction.previousQuantity,
    transaction.previousQuantityOnHand,
    transaction.previousStock,
    transaction.oldQuantity,
    '-',
  );
}

export function getHistoryNewQuantity(transaction) {
  return firstDefined(
    transaction.newQuantity,
    transaction.quantityOnHandAfter,
    transaction.newStock,
    transaction.resultingQuantity,
    '-',
  );
}

export function getHistoryReason(transaction) {
  return firstDefined(transaction.reason, transaction.notes, '-');
}

export function getHistoryResponsibleUser(transaction) {
  return firstDefined(
    transaction.responsibleUserName,
    transaction.responsibleUserEmail,
    transaction.performedByUserEmail,
    transaction.responsibleUser,
    transaction.userName,
    transaction.userEmail,
    '-',
  );
}
