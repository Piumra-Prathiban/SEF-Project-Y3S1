const HIDDEN_REASONING_KEYS = new Set([
  'chainOfThought',
  'chain_of_thought',
  'hiddenReasoning',
  'hidden_reasoning',
  'reasoning',
  'thoughts',
  'internalReasoning',
  'internal_reasoning',
]);

export const INVENTORY_AGENT_STATUSES = [
  'Pending',
  'Running',
  'Validation',
  'PendingApproval',
  'Approved',
  'Rejected',
  'RevisionRequested',
  'Completed',
  'Failed',
];

export function validateWorkflowRequest(request) {
  const errors = [];

  if (!request.objective?.trim()) {
    errors.push('Workflow objective is required.');
  }

  if (request.objective && request.objective.length > 1000) {
    errors.push('Workflow objective must be 1000 characters or fewer.');
  }

  return errors;
}

export function buildWorkflowRequest(request) {
  const variantIds = request.variantIds
    ?.split(',')
    .map((id) => id.trim())
    .filter(Boolean) ?? [];

  return {
    objective: request.objective.trim(),
    variantIds,
  };
}

export function getWorkflowId(workflow) {
  return workflow?.workflowId ?? workflow?.id ?? workflow?.workflow?.id ?? '';
}

export function getWorkflowStatus(workflow) {
  return workflow?.status ?? workflow?.currentStatus ?? workflow?.state ?? 'Pending';
}

export function getWorkflowObjective(workflow) {
  return workflow?.objective ?? workflow?.request?.objective ?? '-';
}

export function getApprovalStatus(workflow) {
  return workflow?.approvalStatus ?? workflow?.approval?.status ?? getWorkflowStatus(workflow);
}

export function getWorkflowPlan(workflow) {
  return workflow?.plan ?? workflow?.structuredPlan ?? workflow?.steps ?? [];
}

export function getCompletedSteps(workflow) {
  return workflow?.completedSteps ?? workflow?.execution?.completedSteps ?? [];
}

export function getToolSummaries(workflow) {
  return workflow?.toolExecutionSummaries
    ?? workflow?.toolResults
    ?? workflow?.execution?.toolSummaries
    ?? [];
}

export function getValidationResult(workflow) {
  return workflow?.validationResult ?? workflow?.validation ?? null;
}

export function getRecommendations(workflow) {
  return workflow?.recommendations
    ?? workflow?.agentOutput?.recommendations
    ?? workflow?.output?.recommendations
    ?? [];
}

export function getRecommendationVariantId(recommendation) {
  return recommendation?.variantId
    ?? recommendation?.productVariantId
    ?? recommendation?.variant?.id
    ?? '-';
}

export function getRecommendationProduct(recommendation) {
  return recommendation?.productName
    ?? recommendation?.product?.name
    ?? recommendation?.variant?.productName
    ?? '-';
}

export function getRecommendationSku(recommendation) {
  return recommendation?.sku ?? recommendation?.variant?.sku ?? '-';
}

export function getRecommendationSize(recommendation) {
  return recommendation?.sizeName
    ?? recommendation?.size?.name
    ?? recommendation?.variant?.sizeName
    ?? '-';
}

export function getRecommendationColour(recommendation) {
  return recommendation?.colourName
    ?? recommendation?.colorName
    ?? recommendation?.colour?.name
    ?? recommendation?.variant?.colourName
    ?? '-';
}

export function getRecommendationCurrentStock(recommendation) {
  return recommendation?.currentStock
    ?? recommendation?.quantityOnHand
    ?? recommendation?.currentQuantity
    ?? '-';
}

export function getRecommendationReorderLevel(recommendation) {
  return recommendation?.reorderLevel ?? recommendation?.minimumStockLevel ?? '-';
}

export function getRecommendationAction(recommendation) {
  return recommendation?.recommendedAction ?? recommendation?.action ?? '-';
}

export function getRecommendationProposedQuantity(recommendation) {
  return recommendation?.proposedQuantity
    ?? recommendation?.recommendedQuantity
    ?? recommendation?.quantity
    ?? '-';
}

export function getRecommendationReason(recommendation) {
  return recommendation?.reason ?? recommendation?.recommendationReason ?? '-';
}

export function getAffectedVariantIds(workflow) {
  return [
    ...new Set(
      getRecommendations(workflow)
        .map((recommendation) => getRecommendationVariantId(recommendation))
        .filter((variantId) => variantId && variantId !== '-'),
    ),
  ];
}

export function getFinalOutcome(workflow) {
  return workflow?.finalOutcome ?? workflow?.outcome ?? workflow?.executionSummary ?? null;
}

export function getWorkflowErrors(workflow) {
  return workflow?.errors ?? workflow?.errorSummary ?? workflow?.error ?? null;
}

export function canReviewWorkflow(user, approverRoles) {
  return approverRoles.includes(user?.role);
}

export function buildApprovalPayload(note) {
  return {
    note: note?.trim() || null,
  };
}

export function buildRejectionPayload(reason) {
  return {
    reason: reason?.trim() || 'Rejected from frontend review.',
  };
}

export function buildRevisionPayload(revisionRequest) {
  return {
    revisionRequest: revisionRequest.trim(),
  };
}

export function getReviewSuccessMessage(action) {
  if (action === 'approve') {
    return 'Workflow approved. Inventory and stock history were refreshed from the backend.';
  }

  if (action === 'reject') {
    return 'Workflow rejected. No stock modification was executed from React.';
  }

  return 'Workflow revision requested. The latest backend workflow state is displayed.';
}

export function normalizeAgentApiError(error) {
  if (error?.errors) {
    return Object.values(error.errors).flat().join(' ');
  }

  return error?.detail || error?.message || 'Something went wrong.';
}

export function sanitizeStructuredValue(value) {
  if (Array.isArray(value)) {
    return value.map((item) => sanitizeStructuredValue(item));
  }

  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value)
        .filter(([key]) => !HIDDEN_REASONING_KEYS.has(key))
        .map(([key, item]) => [key, sanitizeStructuredValue(item)]),
    );
  }

  return value;
}

export function formatStructuredValue(value) {
  if (value === undefined || value === null || value === '') {
    return '-';
  }

  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
    return String(value);
  }

  return JSON.stringify(sanitizeStructuredValue(value), null, 2);
}
