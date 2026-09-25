import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
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
    assert.deepEqual(
      validateWorkflowRequest({ objective: ' ', variantIds: '' }),
      ['Workflow objective is required.'],
    );
  });

  it('builds a strict workflow request from form state', () => {
    assert.deepEqual(
      buildWorkflowRequest({
        objective: ' Analyze low stock ',
        variantIds: ' variant-1, variant-2 ,, ',
      }),
      {
        objective: 'Analyze low stock',
        variantIds: ['variant-1', 'variant-2'],
      },
    );
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

    assert.equal(getWorkflowId(workflow), 'wf-1');
    assert.equal(getWorkflowStatus(workflow), 'PendingApproval');
    assert.equal(getApprovalStatus(workflow), 'PendingApproval');
    assert.deepEqual(getValidationResult(workflow), { isValid: true });
    assert.deepEqual(getToolSummaries(workflow), [{ tool: 'GetLowStockProducts' }]);
    assert.deepEqual(getRecommendations(workflow), [{ variantId: 'variant-1' }]);
  });

  it('removes hidden reasoning fields from structured display values', () => {
    assert.deepEqual(
      sanitizeStructuredValue({
        visible: 'summary',
        chainOfThought: 'hidden',
        nested: {
          reasoning: 'hidden',
          result: 'ok',
        },
      }),
      {
        visible: 'summary',
        nested: {
          result: 'ok',
        },
      },
    );
  });

  it('formats structured values without hidden reasoning', () => {
    const formatted = formatStructuredValue({
      recommendation: 'RESTOCK',
      hiddenReasoning: 'do not show',
    });

    assert.equal(formatted.includes('RESTOCK'), true);
    assert.equal(formatted.includes('do not show'), false);
  });

  it('allows authorized staff/admin approval controls and blocks unauthorized users', () => {
    const approverRoles = ['Staff', 'Administrator'];

    assert.equal(canReviewWorkflow({ role: 'Administrator' }, approverRoles), true);
    assert.equal(canReviewWorkflow({ role: 'Staff' }, approverRoles), true);
    assert.equal(canReviewWorkflow({ role: 'Customer' }, approverRoles), false);
  });

  it('builds approval, rejection and revision payloads for backend endpoints', () => {
    assert.deepEqual(buildApprovalPayload(' Approved '), { note: 'Approved' });
    assert.deepEqual(buildRejectionPayload(' Unsafe '), { reason: 'Unsafe' });
    assert.deepEqual(buildRevisionPayload(' Re-check size M '), {
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

    assert.equal(getRecommendationProduct(recommendation), 'Classic Cotton T-Shirt');
    assert.equal(getRecommendationSku(recommendation), 'TSH-B-M');
    assert.equal(getRecommendationCurrentStock(recommendation), 3);
    assert.equal(getRecommendationReorderLevel(recommendation), 5);
    assert.equal(getRecommendationAction(recommendation), 'RESTOCK');
    assert.equal(getRecommendationProposedQuantity(recommendation), 20);
    assert.equal(getRecommendationReason(recommendation), 'Below reorder level');
  });

  it('identifies affected variants for successful state refresh after approval', () => {
    assert.deepEqual(
      getAffectedVariantIds({
        recommendations: [
          { variantId: 'variant-1' },
          { productVariantId: 'variant-2' },
          { variantId: 'variant-1' },
        ],
      }),
      ['variant-1', 'variant-2'],
    );
  });

  it('returns explicit review result messages for approval, rejection and revision', () => {
    assert.equal(
      getReviewSuccessMessage('approve'),
      'Workflow approved. Inventory and stock history were refreshed from the backend.',
    );
    assert.equal(
      getReviewSuccessMessage('reject'),
      'Workflow rejected. No stock modification was executed from React.',
    );
    assert.equal(
      getReviewSuccessMessage('revise'),
      'Workflow revision requested. The latest backend workflow state is displayed.',
    );
  });

  it('normalizes API failures from workflow approval endpoints', () => {
    assert.equal(
      normalizeAgentApiError({
        errors: {
          reason: ['Revision request is required.'],
        },
      }),
      'Revision request is required.',
    );

    assert.equal(
      normalizeAgentApiError({ detail: 'Workflow is no longer pending approval.' }),
      'Workflow is no longer pending approval.',
    );
  });
});
