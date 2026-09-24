import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildProductPayload,
  validateProductForm,
} from './productFormUtils.js';

describe('product form utilities', () => {
  it('validates product creation requirements', () => {
    assert.deepEqual(
      validateProductForm({
        name: '',
        categoryId: '',
        collectionId: '',
      }),
      [
        'Product name is required.',
        'Category is required.',
        'Collection is required.',
      ],
    );
  });

  it('builds product create/edit payloads with trimmed optional fields', () => {
    assert.deepEqual(
      buildProductPayload({
        name: ' Classic Cotton T-Shirt ',
        description: ' Everyday cotton tee ',
        categoryId: 'category-1',
        collectionId: 'collection-1',
        supplierId: '',
        isActive: false,
      }),
      {
        name: 'Classic Cotton T-Shirt',
        description: 'Everyday cotton tee',
        categoryId: 'category-1',
        collectionId: 'collection-1',
        supplierId: null,
        isActive: false,
      },
    );
  });
});
