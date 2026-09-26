export function validateVariantForm(variant, sizes, colours, isEdit = false) {
  const errors = [];
  const price = Number(variant.price);
  const reorderLevel = Number(variant.reorderLevel);
  const initialQuantity = Number(variant.initialQuantityOnHand);

  if (!variant.sku?.trim()) {
    errors.push('SKU is required.');
  }

  if (!variant.name?.trim()) {
    errors.push('Variant name is required.');
  }

  if (!variant.sizeId) {
    errors.push('Size is required.');
  } else if (!sizes.some((size) => String(size.id) === String(variant.sizeId))) {
    errors.push('Selected size is not valid.');
  }

  if (!variant.colourId) {
    errors.push('Colour is required.');
  } else if (!colours.some((colour) => String(colour.id) === String(variant.colourId))) {
    errors.push('Selected colour is not valid.');
  }

  if (variant.price === '' || Number.isNaN(price)) {
    errors.push('Price is required.');
  } else if (price < 0) {
    errors.push('Price must not be negative.');
  }

  if (variant.reorderLevel === '' || !Number.isInteger(reorderLevel) || reorderLevel < 0) {
    errors.push('Reorder level must be a whole number of zero or more.');
  }

  if (!isEdit && (variant.initialQuantityOnHand === '' || !Number.isInteger(initialQuantity) || initialQuantity < 0)) {
    errors.push('Initial stock must be a whole number of zero or more.');
  }

  return errors;
}

export function buildVariantPayload(variant, isEdit = false) {
  const payload = {
    sku: variant.sku.trim(),
    name: variant.name.trim(),
    sizeId: variant.sizeId,
    colourId: variant.colourId,
    price: Number(variant.price),
    isActive: variant.isActive,
    reorderLevel: Number(variant.reorderLevel),
  };

  if (!isEdit) payload.initialQuantityOnHand = Number(variant.initialQuantityOnHand);
  return payload;
}

export function getVariantStock(variant) {
  return variant.quantityOnHand
    ?? variant.inventoryStock?.quantityOnHand
    ?? variant.inventory?.quantityOnHand
    ?? variant.stock?.quantityOnHand
    ?? '-';
}

export function getVariantSizeName(variant) {
  return variant.sizeName ?? variant.size?.name ?? variant.sizeCode ?? '-';
}

export function getVariantColourName(variant) {
  return variant.colourName ?? variant.colour?.name ?? variant.colorName ?? '-';
}

export function normalizeProductList(response) {
  if (Array.isArray(response)) {
    return response;
  }

  return response?.items ?? [];
}
