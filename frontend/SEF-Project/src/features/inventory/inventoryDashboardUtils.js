function firstDefined(...values) {
  return values.find((value) => value !== undefined && value !== null);
}

export function normalizeInventoryItems(response) {
  if (Array.isArray(response)) {
    return response;
  }

  return response?.items ?? [];
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
