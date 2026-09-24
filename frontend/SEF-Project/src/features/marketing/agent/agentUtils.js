import { formatMoney } from '../marketingUtils';
import {
  APPROVAL_STATUS_INFO,
  APPROVAL_STATUSES,
  IMPACT_LEVEL_INFO,
  STEP_STATUS_INFO,
  STOCK_STATUS_INFO,
  TOOL_STATUS_INFO,
  WORKFLOW_STATUS_INFO,
  WORKFLOW_STATUSES,
} from './agentConstants';

// ---- Status/tone lookups (numeric enums, matching the API) ----------------

function lookup(table, value) {
  return table[value] ?? { label: 'Unknown', tone: 'muted' };
}

export const workflowStatusInfo = (status) => lookup(WORKFLOW_STATUS_INFO, status);
export const stepStatusInfo = (status) => lookup(STEP_STATUS_INFO, status);
export const toolStatusInfo = (status) => lookup(TOOL_STATUS_INFO, status);
export const approvalStatusInfo = (status) => lookup(APPROVAL_STATUS_INFO, status);
export const impactLevelInfo = (impact) => lookup(IMPACT_LEVEL_INFO, impact);

// Tool JSON reports stock status by name (e.g. "LowStock"); accept that or
// the numeric form so this keeps working if a caller passes either.
const STOCK_STATUS_NAMES = { InStock: 0, LowStock: 1, OutOfStock: 2 };

export function stockStatusInfo(value) {
  const index = typeof value === 'string' ? STOCK_STATUS_NAMES[value] : value;
  return lookup(STOCK_STATUS_INFO, index);
}

const TREND_LABELS = {
  NoSales: 'No sales',
  New: 'New',
  Rising: 'Rising',
  Stable: 'Stable',
  Falling: 'Falling',
};

export function formatTrend(value) {
  return TREND_LABELS[value] ?? value ?? '—';
}

// ---- Dates -----------------------------------------------------------------

const timestampFormatter = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'UTC',
});

export function formatTimestamp(value) {
  return value ? `${timestampFormatter.format(new Date(value))} UTC` : '—';
}

// ---- Offers ------------------------------------------------------------------

// Tool/proposal JSON carries the promotion type by name ("PercentageDiscount",
// "FixedAmountDiscount", …), not the numeric Promotion.type enum used
// elsewhere in this feature.
export function formatOffer(promotionType, discountValue) {
  switch (promotionType) {
    case 'FixedAmountDiscount':
      return `${formatMoney(discountValue)} off`;
    case 'FreeShipping':
      return 'Free shipping';
    case 'BuyXGetY':
      return 'Buy X get Y';
    default:
      return `${discountValue}% off`;
  }
}

// ---- Approval workflow state ------------------------------------------------

export function isAwaitingApproval(workflow) {
  return workflow?.status === WORKFLOW_STATUSES.AWAITING_APPROVAL;
}

// The one Approval record still open for a decision, or undefined.
export function getPendingApproval(workflow) {
  return workflow?.approvals?.find((a) => a.status === APPROVAL_STATUSES.PENDING);
}

// Whether `userRole` may click Approve right now, and why not otherwise.
// The server re-checks this; the UI only explains the rule in advance.
export function canApprove(workflow, userRole) {
  if (!isAwaitingApproval(workflow)) {
    return { allowed: false, reason: 'This workflow is not awaiting approval.' };
  }

  if (workflow.impactLevel === 1 && userRole !== 'Administrator') {
    return {
      allowed: false,
      reason: 'High-impact proposals (a large discount or many products) need an Administrator.',
    };
  }

  return { allowed: true, reason: null };
}

// ---- Reading tool results ----------------------------------------------------

// The latest successful execution of `toolName`, or undefined. Tool results
// are plain JSON already parsed by the API client.
export function latestToolResult(workflow, toolName) {
  const executions = (workflow?.toolExecutions ?? [])
    .filter((t) => t.toolName === toolName && t.status === 1 && t.result)
    .sort((a, b) => new Date(a.startedAt ?? 0) - new Date(b.startedAt ?? 0));

  return executions.at(-1)?.result;
}

export function salesVelocityItems(workflow) {
  return latestToolResult(workflow, 'GetSalesVelocity')?.items ?? [];
}

export function inventoryItems(workflow) {
  return latestToolResult(workflow, 'GetInventory')?.items ?? [];
}

export function activePromotionItems(workflow) {
  return latestToolResult(workflow, 'GetActivePromotions')?.items ?? [];
}

export function salesVelocityForProduct(workflow, productId) {
  return salesVelocityItems(workflow).find((item) => item.productId === productId);
}

export function inventoryForProduct(workflow, productId) {
  return inventoryItems(workflow).filter((item) => item.productId === productId);
}

export function pricingForProduct(workflow, productId) {
  return workflow?.pricing?.find((p) => p.productId === productId);
}

export function promotionTargetsProduct(promotion, productId) {
  return Boolean(promotion.productIds?.includes(productId));
}

// ---- Created promotions -----------------------------------------------------

// Best-effort label for a created promotion: promotions are created in the
// same order as the proposals that produced them, so a same-index proposal
// (when the counts still match) names it; otherwise a generic label is used.
export function labelForCreatedPromotion(workflow, index) {
  const proposals = workflow?.proposal?.proposals ?? [];

  return proposals.length === workflow?.createdPromotionIds?.length
    ? proposals[index]?.productName
    : `Promotion ${index + 1}`;
}

// ---- Revision request --------------------------------------------------------

export const EMPTY_REVISION_FORM = {
  comment: '',
  maxDiscountPercent: '',
  maxProposals: '',
  excludeProductIds: [],
};

export function validateRevisionForm(form) {
  const errors = {};
  const comment = form.comment.trim();

  if (comment.length < 3) {
    errors.comment = 'Explain what should change (at least 3 characters).';
  } else if (comment.length > 1000) {
    errors.comment = 'Comment must be 1000 characters or fewer.';
  }

  if (form.maxDiscountPercent !== '') {
    const value = Number(form.maxDiscountPercent);
    if (!Number.isInteger(value) || value < 1 || value > 50) {
      errors.maxDiscountPercent = 'Enter a whole number between 1 and 50.';
    }
  }

  if (form.maxProposals !== '') {
    const value = Number(form.maxProposals);
    if (!Number.isInteger(value) || value < 1 || value > 10) {
      errors.maxProposals = 'Enter a whole number between 1 and 10.';
    }
  }

  return errors;
}

export function buildRevisionPayload(form) {
  return {
    comment: form.comment.trim(),
    maxDiscountPercent: form.maxDiscountPercent === '' ? null : Number(form.maxDiscountPercent),
    maxProposals: form.maxProposals === '' ? null : Number(form.maxProposals),
    excludeProductIds: form.excludeProductIds,
  };
}

// ---- Start workflow form ------------------------------------------------------

export const EMPTY_START_FORM = {
  objective: 'Find products with declining sales and recommend suitable promotions.',
  focus: '0',
  analysisDays: '30',
  maxProposals: '5',
  maxDiscountPercent: '30',
};

export function validateStartForm(form) {
  const errors = {};
  const objective = form.objective.trim();

  if (objective.length < 10) {
    errors.objective = 'Describe the objective in at least 10 characters.';
  } else if (objective.length > 500) {
    errors.objective = 'Objective must be 500 characters or fewer.';
  }

  const analysisDays = Number(form.analysisDays);
  if (!Number.isInteger(analysisDays) || analysisDays < 7 || analysisDays > 90) {
    errors.analysisDays = 'Enter a whole number between 7 and 90.';
  }

  const maxProposals = Number(form.maxProposals);
  if (!Number.isInteger(maxProposals) || maxProposals < 1 || maxProposals > 10) {
    errors.maxProposals = 'Enter a whole number between 1 and 10.';
  }

  const maxDiscountPercent = Number(form.maxDiscountPercent);
  if (!Number.isInteger(maxDiscountPercent) || maxDiscountPercent < 1 || maxDiscountPercent > 50) {
    errors.maxDiscountPercent = 'Enter a whole number between 1 and 50.';
  }

  return errors;
}

export function buildStartPayload(form) {
  return {
    objective: form.objective.trim(),
    focus: Number(form.focus),
    analysisDays: Number(form.analysisDays),
    maxProposals: Number(form.maxProposals),
    maxDiscountPercent: Number(form.maxDiscountPercent),
  };
}
