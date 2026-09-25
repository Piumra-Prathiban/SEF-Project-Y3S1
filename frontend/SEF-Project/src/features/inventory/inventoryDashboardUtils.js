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

export function buildInventoryQuery(query) {
  return {
    product: query.product,
    sku: query.sku,
    categoryId: query.categoryId,
    stockStatus: query.stockStatus,
    lowStockOnly: query.lowStockOnly ? true : '',
    page: query.page,
    pageSize: query.pageSize,
  };
}

export function buildLowStockQuery(query) {
  return {
    sortBy: query.sortBy,
    sortDirection: query.sortDirection,
    page: query.page,
    pageSize: query.pageSize,
  };
}

export function normalizeInventoryItems(response) {
  if (Array.isArray(response)) {
    return response;
  }

  return response?.items ?? [];
}

export function getPaginationMeta(response, fallbackQuery) {
  if (!response || Array.isArray(response)) {
    return {
      page: fallbackQuery.page,
      pageSize: fallbackQuery.pageSize,
      totalItems: Array.isArray(response) ? response.length : 0,
      totalPages: 1,
    };
  }

  return {
    page: response.page ?? fallbackQuery.page,
    pageSize: response.pageSize ?? fallbackQuery.pageSize,
    totalItems: response.totalItems ?? response.items?.length ?? 0,
    totalPages: response.totalPages ?? 1,
  };
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

export function buildInventorySummary(inventoryItems, lowStockItems, paginationMeta = null) {
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
    totalVariants: paginationMeta?.totalItems ?? inventoryItems.length,
    totalStock,
    lowStockVariants: lowStockItems.length,
    outOfStockVariants,
  };
}

export const STOCK_TRANSACTION_TYPES = [
  { value: 'StockIn', label: 'Stock In' },
  { value: 'StockOut', label: 'Stock Out' },
  { value: 'Adjustment', label: 'Adjustment' },
];

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
  } else if (quantity <= 0) {
    errors.push('Quantity must be greater than zero.');
  }

  if (!adjustment.reason?.trim()) {
    errors.push('Reason is required.');
  }

  return errors;
}

export function buildStockAdjustmentPayload(adjustment) {
  return {
    transactionType: adjustment.transactionType,
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
  return firstDefined(
    transaction.transactionType,
    transaction.type,
    transaction.adjustmentType,
    '-',
  );
}

export function getHistoryQuantity(transaction) {
  return firstDefined(transaction.quantity, transaction.adjustmentQuantity, '-');
}

export function getHistoryPreviousQuantity(transaction) {
  return firstDefined(
    transaction.previousQuantity,
    transaction.previousStock,
    transaction.oldQuantity,
    '-',
  );
}

export function getHistoryNewQuantity(transaction) {
  return firstDefined(
    transaction.newQuantity,
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
    transaction.responsibleUser,
    transaction.userName,
    transaction.userEmail,
    '-',
  );
}
