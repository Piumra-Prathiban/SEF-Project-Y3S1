// Enum values match the ASP.NET Core API (enums are sent as numbers).

export const WORKFLOW_STATUSES = {
  PENDING: 0,
  PLANNING: 1,
  IN_PROGRESS: 2,
  AWAITING_APPROVAL: 3,
  COMPLETED: 4,
  FAILED: 5,
  CANCELLED: 6,
};

export const WORKFLOW_STATUS_INFO = [
  { label: 'Pending', tone: 'muted' },
  { label: 'Planning', tone: 'info' },
  { label: 'In progress', tone: 'info' },
  { label: 'Awaiting approval', tone: 'warning' },
  { label: 'Completed', tone: 'success' },
  { label: 'Failed', tone: 'danger' },
  { label: 'Cancelled', tone: 'muted' },
];

export const STEP_STATUS_INFO = [
  { label: 'Pending', tone: 'muted' },
  { label: 'Running', tone: 'info' },
  { label: 'Completed', tone: 'success' },
  { label: 'Failed', tone: 'danger' },
  { label: 'Skipped', tone: 'muted' },
];

export const TOOL_STATUS_INFO = [
  { label: 'Running', tone: 'info' },
  { label: 'Success', tone: 'success' },
  { label: 'Failed', tone: 'danger' },
];

export const APPROVAL_STATUSES = {
  PENDING: 0,
  APPROVED: 1,
  REJECTED: 2,
  REVISION_REQUESTED: 3,
};

export const APPROVAL_STATUS_INFO = [
  { label: 'Pending', tone: 'warning' },
  { label: 'Approved', tone: 'success' },
  { label: 'Rejected', tone: 'danger' },
  { label: 'Revision requested', tone: 'info' },
];

function lookup(table, value) {
  return table[value] ?? { label: 'Unknown', tone: 'muted' };
}

export const workflowStatusInfo = (status) => lookup(WORKFLOW_STATUS_INFO, status);
export const stepStatusInfo = (status) => lookup(STEP_STATUS_INFO, status);
export const toolStatusInfo = (status) => lookup(TOOL_STATUS_INFO, status);
export const approvalStatusInfo = (status) => lookup(APPROVAL_STATUS_INFO, status);

const timestampFormatter = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'UTC',
});

export function formatTimestamp(value) {
  return value ? `${timestampFormatter.format(new Date(value))} UTC` : '—';
}

export function isAwaitingApproval(workflow) {
  return workflow?.status === WORKFLOW_STATUSES.AWAITING_APPROVAL;
}

export function getPendingApproval(workflow) {
  return workflow?.approvals?.find((a) => a.status === APPROVAL_STATUSES.PENDING);
}

// The workflow response nests the resolution document under `.resolution`,
// and the actual resolution under `.resolution.resolution`.
export function resolutionOf(workflow) {
  return workflow?.resolution?.resolution ?? null;
}

export function targetLabel(resolution) {
  const id = resolution?.targetId ? ` (${resolution.targetId})` : '';
  return `${resolution?.targetType ?? '—'}${id}`;
}

// ---- Start workflow form ------------------------------------------------------

export const EMPTY_START_FORM = {
  objective: 'Resolve this order, which appears stuck.',
  orderId: '',
};

export function validateStartForm(form) {
  const errors = {};
  const objective = form.objective.trim();

  if (objective.length < 10) {
    errors.objective = 'Describe the objective in at least 10 characters.';
  } else if (objective.length > 500) {
    errors.objective = 'Objective must be 500 characters or fewer.';
  }

  if (!form.orderId) {
    errors.orderId = 'Select an order to investigate.';
  }

  return errors;
}

export function buildStartPayload(form) {
  return {
    objective: form.objective.trim(),
    orderId: form.orderId.trim(),
  };
}

// ---- Revision request --------------------------------------------------------

export const EMPTY_REVISION_FORM = { comment: '' };

export function validateRevisionForm(form) {
  const errors = {};
  const comment = form.comment.trim();

  if (comment.length < 3) {
    errors.comment = 'Explain what should change (at least 3 characters).';
  } else if (comment.length > 1000) {
    errors.comment = 'Comment must be 1000 characters or fewer.';
  }

  return errors;
}

export function buildRevisionPayload(form) {
  return { comment: form.comment.trim() };
}
