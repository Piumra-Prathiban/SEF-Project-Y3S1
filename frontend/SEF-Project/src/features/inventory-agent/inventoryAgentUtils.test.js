import { describe, expect, it } from 'vitest';
import {
  buildWorkflowRequest,
  buildApprovalPayload,
  buildRejectionPayload,
  buildRevisionPayload,
  canReviewWorkflow,
  formatStructuredValue,
  getAffectedVariantIds,
  getApprovalStatus,
  getRecommendations,
  getRecommendationAction,
  getRecommendationCurrentStock,
  getRecommendationProduct,
  getRecommendationProposedQuantity,
  getRecommendationReason,
  getRecommendationReorderLevel,
  getRecommendationSku,
  getToolSummaries,
  getValidationResult,
  getWorkflowId,
  getWorkflowStatus,
  getReviewSuccessMessage,
  normalizeAgentApiError,
  sanitizeStructuredValue,
  validateWorkflowRequest,
} from './inventoryAgentUtils.js';

describe('inventory agent utilities', () => {
  it('validates required workflow objective', () => {
    expect(validateWorkflowRequest({ objective: ' ', variantIds: '' })).toEqual([
      'Workflow objective is required.',
    ]);
  });

  it('builds a strict workflow request from form state', () => {
    expect(
      buildWorkflowRequest({
        objective: ' Analyze low stock ',
        variantIds: ' variant-1, variant-2 ,, ',
      }),
    ).toEqual({
      objective: 'Analyze low stock',
      variantIds: ['variant-1', 'variant-2'],
    });
  });

  it('reads common workflow response fields', () => {
    const workflow = {
      workflowId: 'wf-1',
      status: 'PendingApproval',
      approval: { status: 'PendingApproval' },
      validation: { isValid: true },
      toolResults: [{ tool: 'GetLowStockProducts' }],
      agentOutput: {
        recommendations: [{ variantId: 'variant-1' }],
      },
    };

    expect(getWorkflowId(workflow)).toBe('wf-1');
    expect(getWorkflowStatus(workflow)).toBe('PendingApproval');
    expect(getApprovalStatus(workflow)).toBe('PendingApproval');
    expect(getValidationResult(workflow)).toEqual({ isValid: true });
    expect(getToolSummaries(workflow)).toEqual([
      { tool: 'GetLowStockProducts' },
    ]);
    expect(getRecommendations(workflow)).toEqual([{ variantId: 'variant-1' }]);
  });

  it('removes hidden reasoning fields from structured display values', () => {
    expect(
      sanitizeStructuredValue({
        visible: 'summary',
        chainOfThought: 'hidden',
        nested: {
          reasoning: 'hidden',
          result: 'ok',
        },
      }),
    ).toEqual({
      visible: 'summary',
      nested: {
        result: 'ok',
      },
    });
  });

  it('formats structured values without hidden reasoning', () => {
    const formatted = formatStructuredValue({
      recommendation: 'RESTOCK',
      hiddenReasoning: 'do not show',
    });

    expect(formatted.includes('RESTOCK')).toBe(true);
    expect(formatted.includes('do not show')).toBe(false);
  });

  it('allows authorized staff/admin approval controls and blocks unauthorized users', () => {
    const approverRoles = ['Staff', 'Administrator'];

    expect(canReviewWorkflow({ role: 'Administrator' }, approverRoles)).toBe(
      true,
    );
    expect(canReviewWorkflow({ role: 'Staff' }, approverRoles)).toBe(true);
    expect(canReviewWorkflow({ role: 'Customer' }, approverRoles)).toBe(false);
  });

  it('builds approval, rejection and revision payloads for backend endpoints', () => {
    expect(buildApprovalPayload(' Approved ')).toEqual({ note: 'Approved' });
    expect(buildRejectionPayload(' Unsafe ')).toEqual({ reason: 'Unsafe' });
    expect(buildRevisionPayload(' Re-check size M ')).toEqual({
      revisionRequest: 'Re-check size M',
    });
  });

  it('extracts recommendation review fields for the approval table', () => {
    const recommendation = {
      variantId: 'variant-1',
      productName: 'Classic Cotton T-Shirt',
      sku: 'TSH-B-M',
      currentStock: 3,
      reorderLevel: 5,
      recommendedAction: 'RESTOCK',
      proposedQuantity: 20,
      reason: 'Below reorder level',
    };

    expect(getRecommendationProduct(recommendation)).toBe(
      'Classic Cotton T-Shirt',
    );
    expect(getRecommendationSku(recommendation)).toBe('TSH-B-M');
    expect(getRecommendationCurrentStock(recommendation)).toBe(3);
    expect(getRecommendationReorderLevel(recommendation)).toBe(5);
    expect(getRecommendationAction(recommendation)).toBe('RESTOCK');
    expect(getRecommendationProposedQuantity(recommendation)).toBe(20);
    expect(getRecommendationReason(recommendation)).toBe('Below reorder level');
  });

  it('identifies affected variants for successful state refresh after approval', () => {
    expect(
      getAffectedVariantIds({
        recommendations: [
          { variantId: 'variant-1' },
          { productVariantId: 'variant-2' },
          { variantId: 'variant-1' },
        ],
      }),
    ).toEqual(['variant-1', 'variant-2']);
  });

  it('returns explicit review result messages for approval, rejection and revision', () => {
    expect(getReviewSuccessMessage('approve')).toBe(
      'Workflow approved. Inventory and stock history were refreshed from the backend.',
    );
    expect(getReviewSuccessMessage('reject')).toBe(
      'Workflow rejected. No stock modification was executed from React.',
    );
    expect(getReviewSuccessMessage('revise')).toBe(
      'Workflow revision requested. The latest backend workflow state is displayed.',
    );
  });

  it('normalizes API failures from workflow approval endpoints', () => {
    expect(
      normalizeAgentApiError({
        errors: {
          reason: ['Revision request is required.'],
        },
      }),
    ).toBe('Revision request is required.');

    expect(
      normalizeAgentApiError({
        detail: 'Workflow is no longer pending approval.',
      }),
    ).toBe('Workflow is no longer pending approval.');
  });
});
