import { describe, expect, it } from 'vitest';
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

    expect(errors).toEqual([
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

    expect(payload).toEqual({
      sku: 'TSH-B-M',
      sizeId: 'size-1',
      colourId: 'colour-1',
      price: 3500,
      isActive: false,
    });
  });

  it('reads stock and related names from supported response shapes', () => {
    expect(getVariantStock({ inventoryStock: { quantityOnHand: 12 } })).toBe(12);
    expect(getVariantSizeName({ size: { name: 'Large' } })).toBe('Large');
    expect(getVariantColourName({ colourName: 'White' })).toBe('White');
  });

  it('normalizes paginated product responses', () => {
    const products = [{ id: 'product-1' }];

    expect(normalizeProductList({ items: products })).toEqual(products);
    expect(normalizeProductList(products)).toEqual(products);
  });
});
