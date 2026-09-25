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

export const IMPACT_LEVELS = { LOW: 0, HIGH: 1 };

export const IMPACT_LEVEL_INFO = [
  { label: 'Low impact', tone: 'muted' },
  { label: 'High impact', tone: 'warning' },
];

export const AGENT_FOCUS_OPTIONS = [
  { value: 0, label: 'Declining sales' },
  { value: 1, label: 'Slow-moving stock' },
];

export const STOCK_STATUS_INFO = [
  { label: 'In stock', tone: 'success' },
  { label: 'Low stock', tone: 'warning' },
  { label: 'Out of stock', tone: 'danger' },
];

// Only an Administrator may approve a high-impact proposal (server rule).
export const ADMINISTRATOR_ROLE = 'Administrator';
