import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildInventorySummary,
  buildStockAdjustmentPayload,
  getColourName,
  getHistoryNewQuantity,
  getHistoryPreviousQuantity,
  getHistoryReason,
  getHistoryResponsibleUser,
  getHistoryType,
  getInventoryQuantity,
  getInventoryStatus,
  getProductName,
  getReorderLevel,
  getSizeName,
  getSku,
  getVariantId,
  normalizeInventoryItems,
  normalizeStockHistoryItems,
  validateStockAdjustment,
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

  it('validates stock adjustment requests before API submission', () => {
    assert.deepEqual(
      validateStockAdjustment({
        transactionType: 'StockIn',
        quantity: '5',
        reason: 'New delivery',
      }),
      [],
    );

    assert.deepEqual(
      validateStockAdjustment({
        transactionType: 'BadType',
        quantity: '0',
        reason: '',
      }),
      [
        'Transaction type is not valid.',
        'Quantity must be greater than zero.',
        'Reason is required.',
      ],
    );
  });

  it('builds stock adjustment payload without calculating stock on the frontend', () => {
    assert.deepEqual(
      buildStockAdjustmentPayload({
        transactionType: 'StockOut',
        quantity: '3',
        reason: 'Damaged items',
      }),
      {
        transactionType: 'StockOut',
        quantity: 3,
        reason: 'Damaged items',
      },
    );
  });

  it('normalizes stock history and exposes rendering fields', () => {
    const transaction = {
      transactionType: 'StockIn',
      quantity: 5,
      previousQuantity: 10,
      newQuantity: 15,
      reason: 'Restock',
      responsibleUserEmail: 'staff@example.com',
    };

    assert.deepEqual(normalizeStockHistoryItems({ transactions: [transaction] }), [transaction]);
    assert.equal(getHistoryType(transaction), 'StockIn');
    assert.equal(getHistoryPreviousQuantity(transaction), 10);
    assert.equal(getHistoryNewQuantity(transaction), 15);
    assert.equal(getHistoryReason(transaction), 'Restock');
    assert.equal(getHistoryResponsibleUser(transaction), 'staff@example.com');
  });

  it('finds the variant id needed for inventory detail and adjustment APIs', () => {
    assert.equal(getVariantId({ productVariantId: 'variant-1' }), 'variant-1');
    assert.equal(getVariantId({ productVariant: { id: 'variant-2' } }), 'variant-2');
  });
});
