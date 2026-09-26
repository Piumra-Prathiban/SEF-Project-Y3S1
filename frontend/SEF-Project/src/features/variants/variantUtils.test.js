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
  it('validates every required backend field for a new variant', () => {
    const errors = validateVariantForm(
      { sku: '', name: '', sizeId: '', colourId: 'missing', price: '-1', reorderLevel: '-2', initialQuantityOnHand: '-1', isActive: true },
      sizes,
      colours,
    );

    expect(errors).toEqual([
      'SKU is required.',
      'Variant name is required.',
      'Size is required.',
      'Selected colour is not valid.',
      'Price must not be negative.',
      'Reorder level must be a whole number of zero or more.',
      'Initial stock must be a whole number of zero or more.',
    ]);
  });

  it('builds the create payload with name and stock fields required by the API', () => {
    const payload = buildVariantPayload({
      sku: ' TSH-B-M ',
      name: ' Medium / Black ',
      sizeId: 'size-1',
      colourId: 'colour-1',
      price: '3500',
      initialQuantityOnHand: '12',
      reorderLevel: '4',
      isActive: false,
    });

    expect(payload).toEqual({
      sku: 'TSH-B-M',
      name: 'Medium / Black',
      sizeId: 'size-1',
      colourId: 'colour-1',
      price: 3500,
      isActive: false,
      reorderLevel: 4,
      initialQuantityOnHand: 12,
    });
  });

  it('omits initial stock when editing an existing variant', () => {
    const variant = { sku: 'TSH-B-M', name: 'Medium / Black', sizeId: 'size-1', colourId: 'colour-1', price: '3500', reorderLevel: '5', initialQuantityOnHand: '', isActive: true };
    expect(validateVariantForm(variant, sizes, colours, true)).toEqual([]);
    expect(buildVariantPayload(variant, true)).not.toHaveProperty('initialQuantityOnHand');
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
