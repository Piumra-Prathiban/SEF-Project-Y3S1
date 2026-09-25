import { describe, expect, it } from 'vitest';
import {
  buildProductPayload,
  initialProductFormState,
  validateProductForm,
} from './productFormUtils.js';

describe('product form utilities', () => {
  it('validates product creation requirements', () => {
    expect(
      validateProductForm({
        name: '',
        categoryId: '',
        collectionId: '',
      }),
    ).toEqual([
      'Product name is required.',
      'Category is required.',
      'Collection is required.',
    ]);
  });

  it('builds product create/edit payloads with trimmed optional fields', () => {
    expect(
      buildProductPayload({
        name: ' Classic Cotton T-Shirt ',
        description: ' Everyday cotton tee ',
        imageUrl: ' https://cdn.clothic.example/tshirt.jpg ',
        categoryId: 'category-1',
        collectionId: 'collection-1',
        supplierId: '',
        isActive: false,
      }),
    ).toEqual({
      name: 'Classic Cotton T-Shirt',
      description: 'Everyday cotton tee',
      imageUrl: 'https://cdn.clothic.example/tshirt.jpg',
      categoryId: 'category-1',
      collectionId: 'collection-1',
      supplierId: null,
      isActive: false,
    });
  });

  it('sends a null image url when none is provided', () => {
    expect(
      buildProductPayload({
        ...initialProductFormState,
        name: 'Classic Cotton T-Shirt',
        categoryId: 'category-1',
        collectionId: 'collection-1',
      }).imageUrl,
    ).toBeNull();
  });
});
