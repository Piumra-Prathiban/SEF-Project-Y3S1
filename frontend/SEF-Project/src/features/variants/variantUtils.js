export function validateVariantForm(variant, sizes, colours) {
  const errors = [];
  const price = Number(variant.price);

  if (!variant.sku?.trim()) {
    errors.push('SKU is required.');
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

  return errors;
}

export function buildVariantPayload(variant) {
  return {
    sku: variant.sku.trim(),
    sizeId: variant.sizeId,
    colourId: variant.colourId,
    price: Number(variant.price),
    isActive: variant.isActive,
  };
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
