import { describe, expect, it } from 'vitest';
import {
  buildPurchaseOrderPayload,
  calculateLineTotal,
  calculatePurchaseOrderTotal,
  canCancelPurchaseOrder,
  canReceivePurchaseOrder,
  canSubmitPurchaseOrder,
  describePurchaseOrderError,
  emptyPurchaseOrderItem,
  getPurchaseOrderStatusName,
  PurchaseOrderStatus,
  validatePurchaseOrderForm,
} from './purchaseOrderUtils';

describe('purchaseOrderUtils', () => {
  it('calculates line and grand totals', () => {
    expect(calculateLineTotal({ quantity: '3', unitCost: '10.25' })).toBe(30.75);
    expect(calculateLineTotal({ quantity: '', unitCost: '10' })).toBe(0);

    expect(calculatePurchaseOrderTotal([
      { quantity: 3, unitCost: 10.25 },
      { quantity: 2, unitCost: 20.5 },
    ])).toBe(71.75);
  });

  it('describes the lifecycle of a purchase order status', () => {
    expect(getPurchaseOrderStatusName(PurchaseOrderStatus.Draft)).toBe('Draft');
    expect(getPurchaseOrderStatusName(PurchaseOrderStatus.Received)).toBe('Received');

    expect(canSubmitPurchaseOrder(PurchaseOrderStatus.Draft)).toBe(true);
    expect(canSubmitPurchaseOrder(PurchaseOrderStatus.Submitted)).toBe(false);

    expect(canReceivePurchaseOrder(PurchaseOrderStatus.Submitted)).toBe(true);
    expect(canReceivePurchaseOrder(PurchaseOrderStatus.Draft)).toBe(false);

    expect(canCancelPurchaseOrder(PurchaseOrderStatus.Draft)).toBe(true);
    expect(canCancelPurchaseOrder(PurchaseOrderStatus.Submitted)).toBe(true);
    expect(canCancelPurchaseOrder(PurchaseOrderStatus.Received)).toBe(false);
  });

  it('validates the create form', () => {
    const errors = validatePurchaseOrderForm({
      supplierId: '',
      items: [{ productVariantId: '', quantity: '0', unitCost: '-1' }],
    });

    expect(errors).toContain('Select a supplier for this purchase order.');
    expect(errors).toContain('Line 1: select a product variant.');
    expect(errors).toContain('Line 1: quantity must be a whole number of 1 or more.');
    expect(errors).toContain('Line 1: unit cost cannot be negative.');

    expect(validatePurchaseOrderForm({
      supplierId: 'supplier-1',
      items: [{ productVariantId: 'variant-1', quantity: '2', unitCost: '12.5' }],
    })).toEqual([]);

    expect(validatePurchaseOrderForm({ supplierId: 'supplier-1', items: [] }))
      .toContain('Add at least one product variant to the purchase order.');
  });

  it('builds the API payload from form state', () => {
    const payload = buildPurchaseOrderPayload({
      supplierId: 'supplier-1',
      expectedAt: '2026-03-01',
      notes: '  Spring restock  ',
      items: [{ productVariantId: 'variant-1', quantity: '2', unitCost: '10.5' }],
    });

    expect(payload).toEqual({
      supplierId: 'supplier-1',
      expectedAt: new Date('2026-03-01').toISOString(),
      notes: 'Spring restock',
      items: [{ productVariantId: 'variant-1', quantity: 2, unitCost: 10.5 }],
    });

    expect(buildPurchaseOrderPayload({
      supplierId: 'supplier-1',
      expectedAt: '',
      notes: '   ',
      items: [],
    })).toEqual({
      supplierId: 'supplier-1',
      expectedAt: null,
      notes: null,
      items: [],
    });
  });

  it('describes API errors on-brand', () => {
    expect(describePurchaseOrderError({ status: 403 }, 'receive'))
      .toMatch(/permission/i);
    expect(describePurchaseOrderError({ status: 404 }, 'submit'))
      .toMatch(/could not be found/i);
    expect(describePurchaseOrderError(
      { status: 409, message: 'Transition from Received to Submitted is not allowed.' },
      'submit',
    )).toBe('Transition from Received to Submitted is not allowed.');
    expect(describePurchaseOrderError({ status: 500, message: 'Boom' }, 'cancel'))
      .toBe('Boom');
  });

  it('exposes a blank line guard', () => {
    expect(emptyPurchaseOrderItem()).toEqual({
      productVariantId: '',
      quantity: '1',
      unitCost: '',
    });
  });
});
