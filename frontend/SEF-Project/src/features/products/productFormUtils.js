export const initialProductFormState = {
  name: '',
  description: '',
  imageUrl: '',
  categoryId: '',
  collectionId: '',
  supplierId: '',
  isActive: true,
};

export function validateProductForm(product) {
  const errors = [];

  if (!product.name?.trim()) {
    errors.push('Product name is required.');
  }

  if (!product.categoryId) {
    errors.push('Category is required.');
  }

  if (!product.collectionId) {
    errors.push('Collection is required.');
  }

  return errors;
}

export function buildProductPayload(product) {
  return {
    name: product.name.trim(),
    description: product.description.trim() || null,
    imageUrl: product.imageUrl?.trim() || null,
    categoryId: product.categoryId,
    collectionId: product.collectionId,
    supplierId: product.supplierId || null,
    isActive: product.isActive,
  };
}
