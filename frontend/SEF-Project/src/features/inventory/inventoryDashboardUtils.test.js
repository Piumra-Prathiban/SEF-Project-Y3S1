import { describe, expect, it } from 'vitest';
import {
  buildInventorySummary,
  buildInventoryQuery,
  buildLowStockQuery,
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
  getPaginationMeta,
  normalizeInventoryItems,
  normalizeStockHistoryItems,
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

  it('passes server-side inventory query parameters through to the API', () => {
    expect(
      buildInventoryQuery({
        ...defaultInventoryQuery,
        product: 'shirt',
        sku: 'TSH',
        categoryId: 'category-1',
        stockStatus: 'Low Stock',
        lowStockOnly: true,
        page: 3,
        pageSize: 25,
      }),
    ).toEqual({
      product: 'shirt',
      sku: 'TSH',
      categoryId: 'category-1',
      stockStatus: 'Low Stock',
      lowStockOnly: true,
      page: 3,
      pageSize: 25,
    });
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

  it('passes server-side low-stock sorting and pagination parameters through to the API', () => {
    expect(
      buildLowStockQuery({
        ...defaultLowStockQuery,
        sortBy: 'quantity',
        sortDirection: 'desc',
        page: 2,
        pageSize: 25,
      }),
    ).toEqual({
      sortBy: 'quantity',
      sortDirection: 'desc',
      page: 2,
      pageSize: 25,
    });
  });

  it('extracts pagination metadata from server responses', () => {
    expect(
      getPaginationMeta(
        {
          items: [{ id: 'stock-1' }],
          page: 2,
          pageSize: 25,
          totalItems: 60,
          totalPages: 3,
        },
        defaultInventoryQuery,
      ),
    ).toEqual({
      page: 2,
      pageSize: 25,
      totalItems: 60,
      totalPages: 3,
    });
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
      { totalItems: 10 },
    );

    expect(summary).toEqual({
      totalProducts: 2,
      totalVariants: 10,
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
      'Quantity must be greater than zero.',
      'Reason is required.',
    ]);
  });

  it('builds stock adjustment payload without calculating stock on the frontend', () => {
    expect(
      buildStockAdjustmentPayload({
        transactionType: 'StockOut',
        quantity: '3',
        reason: 'Damaged items',
      }),
    ).toEqual({
      transactionType: 'StockOut',
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
