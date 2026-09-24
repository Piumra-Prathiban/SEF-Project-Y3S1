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

export function getFinalOutcome(workflow) {
  return workflow?.finalOutcome ?? workflow?.outcome ?? workflow?.executionSummary ?? null;
}

export function getWorkflowErrors(workflow) {
  return workflow?.errors ?? workflow?.errorSummary ?? workflow?.error ?? null;
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
