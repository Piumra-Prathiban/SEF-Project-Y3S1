import { describe, expect, it } from 'vitest';
import { formatMoney } from '../marketingUtils';
import {
  buildRevisionPayload,
  buildStartPayload,
  canApprove,
  EMPTY_REVISION_FORM,
  EMPTY_START_FORM,
  formatOffer,
  formatTimestamp,
  formatTrend,
  getPendingApproval,
  impactLevelInfo,
  inventoryForProduct,
  isAwaitingApproval,
  labelForCreatedPromotion,
  latestToolResult,
  pricingForProduct,
  promotionTargetsProduct,
  salesVelocityForProduct,
  stepStatusInfo,
  stockStatusInfo,
  toolStatusInfo,
  validateRevisionForm,
  validateStartForm,
  workflowStatusInfo,
} from './agentUtils';

describe('status lookups', () => {
  it('maps known numeric enum values to a label and tone', () => {
    expect(workflowStatusInfo(3)).toEqual({ label: 'Awaiting approval', tone: 'warning' });
    expect(workflowStatusInfo(4)).toEqual({ label: 'Completed', tone: 'success' });
    expect(stepStatusInfo(3)).toMatchObject({ label: 'Failed', tone: 'danger' });
    expect(toolStatusInfo(1)).toMatchObject({ label: 'Success', tone: 'success' });
    expect(impactLevelInfo(1)).toMatchObject({ label: 'High impact' });
  });

  it('falls back to Unknown for an unrecognised value', () => {
    expect(workflowStatusInfo(99)).toEqual({ label: 'Unknown', tone: 'muted' });
    expect(workflowStatusInfo(undefined)).toEqual({ label: 'Unknown', tone: 'muted' });
  });

  it('reads stock status from the tool JSON string, not just a number', () => {
    expect(stockStatusInfo('LowStock')).toMatchObject({ label: 'Low stock', tone: 'warning' });
    expect(stockStatusInfo('OutOfStock')).toMatchObject({ label: 'Out of stock', tone: 'danger' });
    expect(stockStatusInfo(0)).toMatchObject({ label: 'In stock' });
  });

  it('formats a demand trend into a readable label', () => {
    expect(formatTrend('NoSales')).toBe('No sales');
    expect(formatTrend('Rising')).toBe('Rising');
    expect(formatTrend(undefined)).toBe('—');
  });
});

describe('formatTimestamp', () => {
  it('renders a UTC date and time', () => {
    expect(formatTimestamp('2026-10-16T09:05:00Z')).toBe('16 Oct 2026, 09:05 UTC');
  });

  it('renders a dash for a missing value', () => {
    expect(formatTimestamp(null)).toBe('—');
  });
});

describe('formatOffer', () => {
  it.each([
    ['PercentageDiscount', 15, '15% off'],
    ['FixedAmountDiscount', 50, `${formatMoney(50)} off`],
    ['FreeShipping', 0, 'Free shipping'],
    ['BuyXGetY', 0, 'Buy X get Y'],
  ])('describes %s', (type, value, expected) => {
    expect(formatOffer(type, value)).toBe(expected);
  });
});

const baseWorkflow = {
  status: 3, // AwaitingApproval
  impactLevel: 0,
  approvals: [
    { status: 1, requestedAt: '2026-10-15T00:00:00Z', reviewedByUserId: 5, reviewedAt: '2026-10-15T01:00:00Z' },
    { status: 0, requestedAt: '2026-10-16T00:00:00Z' },
  ],
};

describe('workflow decision state', () => {
  it('isAwaitingApproval / getPendingApproval read the current state', () => {
    expect(isAwaitingApproval(baseWorkflow)).toBe(true);
    expect(isAwaitingApproval({ ...baseWorkflow, status: 4 })).toBe(false);
    expect(getPendingApproval(baseWorkflow)).toBe(baseWorkflow.approvals[1]);
    expect(getPendingApproval({ ...baseWorkflow, approvals: [] })).toBeUndefined();
  });

  it('canApprove refuses when the workflow is not awaiting approval', () => {
    const result = canApprove({ ...baseWorkflow, status: 4 }, 'Administrator');
    expect(result).toEqual({ allowed: false, reason: expect.stringContaining('not awaiting approval') });
  });

  it('canApprove requires an Administrator for a high-impact proposal', () => {
    const highImpact = { ...baseWorkflow, impactLevel: 1 };

    expect(canApprove(highImpact, 'Staff')).toEqual({
      allowed: false,
      reason: expect.stringContaining('Administrator'),
    });
    expect(canApprove(highImpact, 'Administrator')).toEqual({ allowed: true, reason: null });
  });

  it('canApprove allows Staff for a low-impact proposal', () => {
    expect(canApprove(baseWorkflow, 'Staff')).toEqual({ allowed: true, reason: null });
  });
});

describe('reading tool results', () => {
  const workflow = {
    toolExecutions: [
      {
        toolName: 'GetSalesVelocity',
        status: 1,
        startedAt: '2026-10-15T00:00:00Z',
        result: { items: [{ productId: 'p1', unitsSold: 3, previousUnitsSold: 10 }] },
      },
      {
        toolName: 'GetSalesVelocity',
        status: 2, // Failed - must be ignored
        startedAt: '2026-10-15T00:05:00Z',
        result: { items: [{ productId: 'p1', unitsSold: 999 }] },
      },
      {
        toolName: 'GetSalesVelocity',
        status: 1,
        startedAt: '2026-10-16T00:00:00Z', // latest successful call (a revision re-run)
        result: { items: [{ productId: 'p1', unitsSold: 5, previousUnitsSold: 10 }] },
      },
      {
        toolName: 'GetInventory',
        status: 1,
        startedAt: '2026-10-16T00:00:00Z',
        result: {
          items: [
            { productId: 'p1', productVariantId: 'v1', sku: 'SKU-1', availableQuantity: 2 },
            { productId: 'p2', productVariantId: 'v2', sku: 'SKU-2', availableQuantity: 9 },
          ],
        },
      },
    ],
    pricing: [{ productId: 'p1', variants: [{ productVariantId: 'v1', finalPrice: 900 }] }],
  };

  it('latestToolResult picks the newest successful call and ignores failures', () => {
    expect(latestToolResult(workflow, 'GetSalesVelocity').items[0].unitsSold).toBe(5);
  });

  it('salesVelocityForProduct / inventoryForProduct filter by productId', () => {
    expect(salesVelocityForProduct(workflow, 'p1').unitsSold).toBe(5);
    expect(salesVelocityForProduct(workflow, 'missing')).toBeUndefined();
    expect(inventoryForProduct(workflow, 'p1')).toEqual([workflow.toolExecutions[3].result.items[0]]);
  });

  it('pricingForProduct finds the matching product', () => {
    expect(pricingForProduct(workflow, 'p1').variants[0].finalPrice).toBe(900);
    expect(pricingForProduct(workflow, 'missing')).toBeUndefined();
  });

  it('promotionTargetsProduct checks the promotion product list', () => {
    expect(promotionTargetsProduct({ productIds: ['p1'] }, 'p1')).toBe(true);
    expect(promotionTargetsProduct({ productIds: ['p2'] }, 'p1')).toBe(false);
    expect(promotionTargetsProduct({}, 'p1')).toBe(false);
  });
});

describe('labelForCreatedPromotion', () => {
  it('names the promotion by its proposal when the counts still match', () => {
    const workflow = {
      proposal: { proposals: [{ productName: 'Spaghetti Carbonara' }] },
      createdPromotionIds: ['promo-1'],
    };

    expect(labelForCreatedPromotion(workflow, 0)).toBe('Spaghetti Carbonara');
  });

  it('falls back to a generic label when the counts no longer match', () => {
    const workflow = {
      proposal: { proposals: [{ productName: 'A' }, { productName: 'B' }] },
      createdPromotionIds: ['promo-1'],
    };

    expect(labelForCreatedPromotion(workflow, 0)).toBe('Promotion 1');
  });
});

describe('revision form', () => {
  it('requires a comment of a sensible length', () => {
    expect(validateRevisionForm(EMPTY_REVISION_FORM)).toHaveProperty('comment');
    expect(validateRevisionForm({ ...EMPTY_REVISION_FORM, comment: 'Please revise.' })).toEqual({});
    expect(
      validateRevisionForm({ ...EMPTY_REVISION_FORM, comment: 'x'.repeat(1001) })
    ).toHaveProperty('comment');
  });

  it('validates optional numeric constraints', () => {
    const form = { ...EMPTY_REVISION_FORM, comment: 'Tighten the limits.', maxDiscountPercent: '99' };
    expect(validateRevisionForm(form)).toHaveProperty('maxDiscountPercent');
  });

  it('builds the API payload, sending null for unset limits', () => {
    const form = {
      comment: ' Keep discounts small. ',
      maxDiscountPercent: '10',
      maxProposals: '',
      excludeProductIds: ['p1'],
    };

    expect(buildRevisionPayload(form)).toEqual({
      comment: 'Keep discounts small.',
      maxDiscountPercent: 10,
      maxProposals: null,
      excludeProductIds: ['p1'],
    });
  });
});

describe('start form', () => {
  it('accepts the default form', () => {
    expect(validateStartForm(EMPTY_START_FORM)).toEqual({});
  });

  it('rejects a short objective and out-of-range numbers', () => {
    const errors = validateStartForm({
      ...EMPTY_START_FORM,
      objective: 'short',
      analysisDays: '500',
      maxProposals: '0',
      maxDiscountPercent: '90',
    });

    expect(Object.keys(errors).sort()).toEqual(
      ['analysisDays', 'maxDiscountPercent', 'maxProposals', 'objective'].sort()
    );
  });

  it('builds a numeric API payload', () => {
    expect(buildStartPayload(EMPTY_START_FORM)).toEqual({
      objective: EMPTY_START_FORM.objective,
      focus: 0,
      analysisDays: 30,
      maxProposals: 5,
      maxDiscountPercent: 30,
    });
  });
});
