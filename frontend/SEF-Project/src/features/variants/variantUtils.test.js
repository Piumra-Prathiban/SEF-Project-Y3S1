import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildVariantPayload,
  getVariantColourName,
  getVariantSizeName,
  getVariantStock,
  normalizeProductList,
  validateVariantForm,
} from './variantUtils.js';

const sizes = [{ id: 'size-1', name: 'Medium' }];
const colours = [{ id: 'colour-1', name: 'Black' }];

describe('variant utilities', () => {
  it('validates required fields and non-negative price', () => {
    const errors = validateVariantForm(
      { sku: '', sizeId: '', colourId: 'missing', price: '-1', isActive: true },
      sizes,
      colours,
    );

    assert.deepEqual(errors, [
      'SKU is required.',
      'Size is required.',
      'Selected colour is not valid.',
      'Price must not be negative.',
    ]);
  });

  it('builds the backend payload with trimmed sku and numeric price', () => {
    const payload = buildVariantPayload({
      sku: ' TSH-B-M ',
      sizeId: 'size-1',
      colourId: 'colour-1',
      price: '3500',
      isActive: false,
    });

    assert.deepEqual(payload, {
      sku: 'TSH-B-M',
      sizeId: 'size-1',
      colourId: 'colour-1',
      price: 3500,
      isActive: false,
    });
  });

  it('reads stock and related names from supported response shapes', () => {
    assert.equal(getVariantStock({ inventoryStock: { quantityOnHand: 12 } }), 12);
    assert.equal(getVariantSizeName({ size: { name: 'Large' } }), 'Large');
    assert.equal(getVariantColourName({ colourName: 'White' }), 'White');
  });

  it('normalizes paginated product responses', () => {
    const products = [{ id: 'product-1' }];

    assert.deepEqual(normalizeProductList({ items: products }), products);
    assert.deepEqual(normalizeProductList(products), products);
  });
});
