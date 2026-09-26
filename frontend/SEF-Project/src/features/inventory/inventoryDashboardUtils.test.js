import { describe, expect, it } from 'vitest';
import {
  buildInventorySummary,
  buildStockAdjustmentPayload,
  defaultInventoryQuery,
  defaultLowStockQuery,
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
  getShortageAmount,
  getSizeName,
  getSku,
  getVariantId,
  filterInventoryItems,
  normalizeInventoryItems,
  normalizeStockHistoryItems,
  paginateItems,
  sortLowStockItems,
  updateInventoryQuery,
  updateLowStockQuery,
  validateStockAdjustment,
} from './inventoryDashboardUtils.js';

describe('inventory dashboard utilities', () => {
  it('normalizes array and paginated inventory responses', () => {
    const items = [{ id: 'stock-1' }];

    expect(normalizeInventoryItems(items)).toEqual(items);
    expect(normalizeInventoryItems({ items })).toEqual(items);
  });

  it('resets pagination when inventory filters change', () => {
    const query = updateInventoryQuery(
      { ...defaultInventoryQuery, page: 4 },
      'sku',
      'TSH',
    );

    expect(query.sku).toBe('TSH');
    expect(query.page).toBe(1);
  });

  it('preserves inventory pagination when the page changes', () => {
    const query = updateInventoryQuery(defaultInventoryQuery, 'page', 2);

    expect(query.page).toBe(2);
  });

  it('filters the full inventory list by product, SKU, category and stock state', () => {
    const items = [
      { productName: 'Cotton Shirt', sku: 'TSH-M', categoryId: 'tops', quantityOnHand: 3, reorderLevel: 5 },
      { productName: 'Cotton Shirt', sku: 'TSH-L', categoryId: 'tops', quantityOnHand: 12, reorderLevel: 5 },
      { productName: 'Wide Trousers', sku: 'TRS-M', categoryId: 'bottoms', quantityOnHand: 0, reorderLevel: 5 },
    ];
    expect(filterInventoryItems(items, { ...defaultInventoryQuery, product: 'shirt', sku: 'm', categoryId: 'tops', stockStatus: 'Low Stock', lowStockOnly: true })).toEqual([items[0]]);
  });

  it('resets pagination when low-stock sorting changes', () => {
    const query = updateLowStockQuery(
      { ...defaultLowStockQuery, page: 3 },
      'sortBy',
      'sku',
    );

    expect(query.sortBy).toBe('sku');
    expect(query.page).toBe(1);
  });

  it('sorts low stock and paginates the returned full list', () => {
    const items = [
      { sku: 'A', productName: 'Shirt', quantityOnHand: 3, reorderLevel: 5 },
      { sku: 'B', productName: 'Jacket', quantityOnHand: 1, reorderLevel: 5 },
      { sku: 'C', productName: 'Dress', quantityOnHand: 0, reorderLevel: 5 },
    ];
    const sorted = sortLowStockItems(items, { ...defaultLowStockQuery, sortBy: 'quantity', sortDirection: 'desc' });
    expect(sorted.map((item) => item.sku)).toEqual(['A', 'B', 'C']);
    expect(paginateItems(sorted, 2, 2)).toMatchObject({ items: [items[2]], page: 2, totalItems: 3, totalPages: 2 });
  });

  it('clamps a page after filters reduce the result count', () => {
    expect(paginateItems([{ id: 'one' }], 9, 10)).toMatchObject({ page: 1, totalPages: 1, totalItems: 1 });
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

    expect(getProductName(item)).toBe('Classic Cotton T-Shirt');
    expect(getSku(item)).toBe('TSH-B-M');
    expect(getSizeName(item)).toBe('M');
    expect(getColourName(item)).toBe('Black');
    expect(getInventoryQuantity(item)).toBe(12);
    expect(getReorderLevel(item)).toBe(5);
  });

  it('uses backend inventory status when supplied', () => {
    expect(getInventoryStatus({ status: 'Backend Status' })).toBe(
      'Backend Status',
    );
  });

  it('derives fallback status from quantity and reorder level', () => {
    expect(getInventoryStatus({ quantityOnHand: 0, reorderLevel: 5 })).toBe(
      'Out of Stock',
    );
    expect(getInventoryStatus({ quantityOnHand: 4, reorderLevel: 5 })).toBe(
      'Low Stock',
    );
    expect(getInventoryStatus({ quantityOnHand: 8, reorderLevel: 5 })).toBe(
      'In Stock',
    );
  });

  it('uses provided shortage amount or safely calculates the reorder shortfall', () => {
    expect(
      getShortageAmount({ shortageAmount: 7, quantityOnHand: 1, reorderLevel: 5 }),
    ).toBe(7);
    expect(getShortageAmount({ quantityOnHand: 3, reorderLevel: 5 })).toBe(2);
    expect(getShortageAmount({ quantityOnHand: 8, reorderLevel: 5 })).toBe(0);
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

    expect(summary).toEqual({
      totalProducts: 2,
      totalVariants: 3,
      totalStock: 13,
      lowStockVariants: 2,
      outOfStockVariants: 1,
    });
  });

  it('validates stock adjustment requests before API submission', () => {
    expect(
      validateStockAdjustment({
        transactionType: 'StockIn',
        quantity: '5',
        reason: 'New delivery',
      }),
    ).toEqual([]);

    expect(
      validateStockAdjustment({
        transactionType: 'BadType',
        quantity: '0',
        reason: '',
      }),
    ).toEqual([
      'Transaction type is not valid.',
      'Quantity must be a whole number greater than zero.',
      'Reason is required.',
    ]);
  });

  it('builds the numeric enum payload required by the stock adjustment endpoint', () => {
    expect(
      buildStockAdjustmentPayload({
        transactionType: 'StockOut',
        quantity: '3',
        reason: 'Damaged items',
      }),
    ).toEqual({
      type: 2,
      quantity: 3,
      reason: 'Damaged items',
    });
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

    expect(normalizeStockHistoryItems({ transactions: [transaction] })).toEqual([
      transaction,
    ]);
    expect(getHistoryType(transaction)).toBe('StockIn');
    expect(getHistoryType({ type: 1 })).toBe('Stock in');
    expect(getHistoryPreviousQuantity({ previousQuantityOnHand: 10 })).toBe(10);
    expect(getHistoryNewQuantity({ quantityOnHandAfter: 15 })).toBe(15);
    expect(getHistoryResponsibleUser({ performedByUserEmail: 'staff@example.com' })).toBe('staff@example.com');
    expect(getHistoryPreviousQuantity(transaction)).toBe(10);
    expect(getHistoryNewQuantity(transaction)).toBe(15);
    expect(getHistoryReason(transaction)).toBe('Restock');
    expect(getHistoryResponsibleUser(transaction)).toBe('staff@example.com');
  });

  it('finds the variant id needed for inventory detail and adjustment APIs', () => {
    expect(getVariantId({ productVariantId: 'variant-1' })).toBe('variant-1');
    expect(getVariantId({ productVariant: { id: 'variant-2' } })).toBe(
      'variant-2',
    );
  });
});
