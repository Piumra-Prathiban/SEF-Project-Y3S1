import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  buildWorkflowRequest,
  formatStructuredValue,
  getApprovalStatus,
  getRecommendations,
  getToolSummaries,
  getValidationResult,
  getWorkflowId,
  getWorkflowStatus,
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
});
