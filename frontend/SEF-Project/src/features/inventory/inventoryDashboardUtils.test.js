import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildInventorySummary,
  getColourName,
  getInventoryQuantity,
  getInventoryStatus,
  getProductName,
  getReorderLevel,
  getSizeName,
  getSku,
  normalizeInventoryItems,
} from './inventoryDashboardUtils.js';

describe('inventory dashboard utilities', () => {
  it('normalizes array and paginated inventory responses', () => {
    const items = [{ id: 'stock-1' }];

    assert.deepEqual(normalizeInventoryItems(items), items);
    assert.deepEqual(normalizeInventoryItems({ items }), items);
  });

  it('reads inventory table fields from supported response shapes', () => {
    const item = {
      productVariant: {
        sku: 'TSH-B-M',
        product: { name: 'Classic Cotton T-Shirt' },
        size: { name: 'M' },
        colour: { name: 'Black' },
      },
      inventoryStock: {
        quantityOnHand: 12,
        reorderLevel: 5,
      },
    };

    assert.equal(getProductName(item), 'Classic Cotton T-Shirt');
    assert.equal(getSku(item), 'TSH-B-M');
    assert.equal(getSizeName(item), 'M');
    assert.equal(getColourName(item), 'Black');
    assert.equal(getInventoryQuantity(item), 12);
    assert.equal(getReorderLevel(item), 5);
  });

  it('uses backend inventory status when supplied', () => {
    assert.equal(getInventoryStatus({ status: 'Backend Status' }), 'Backend Status');
  });

  it('derives fallback status from quantity and reorder level', () => {
    assert.equal(getInventoryStatus({ quantityOnHand: 0, reorderLevel: 5 }), 'Out of Stock');
    assert.equal(getInventoryStatus({ quantityOnHand: 4, reorderLevel: 5 }), 'Low Stock');
    assert.equal(getInventoryStatus({ quantityOnHand: 8, reorderLevel: 5 }), 'In Stock');
  });

  it('builds reliable summary metrics from inventory and low-stock data', () => {
    const summary = buildInventorySummary(
      [
        { productId: 'product-1', quantityOnHand: 10 },
        { productId: 'product-1', quantityOnHand: 0 },
        { productId: 'product-2', quantityOnHand: 3 },
      ],
      [{ id: 'low-1' }, { id: 'low-2' }],
    );

    assert.deepEqual(summary, {
      totalProducts: 2,
      totalVariants: 3,
      totalStock: 13,
      lowStockVariants: 2,
      outOfStockVariants: 1,
    });
  });
});
