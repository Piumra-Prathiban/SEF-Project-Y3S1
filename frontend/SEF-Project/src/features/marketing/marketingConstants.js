// Enum values match the ASP.NET Core API (enums are sent as numbers).

export const PROMOTION_TYPES = {
  PERCENTAGE: 0,
  FIXED_AMOUNT: 1,
  BUY_X_GET_Y: 2,
  FREE_SHIPPING: 3,
};

export const PROMOTION_TYPE_OPTIONS = [
  { value: PROMOTION_TYPES.PERCENTAGE, label: 'Percentage discount' },
  { value: PROMOTION_TYPES.FIXED_AMOUNT, label: 'Fixed amount discount' },
  { value: PROMOTION_TYPES.BUY_X_GET_Y, label: 'Buy X get Y' },
  { value: PROMOTION_TYPES.FREE_SHIPPING, label: 'Free shipping' },
];

export const CAMPAIGN_STATUSES = {
  DRAFT: 0,
  SCHEDULED: 1,
  ACTIVE: 2,
  PAUSED: 3,
  COMPLETED: 4,
  CANCELLED: 5,
};

export const CAMPAIGN_STATUS_OPTIONS = [
  { value: CAMPAIGN_STATUSES.DRAFT, label: 'Draft' },
  { value: CAMPAIGN_STATUSES.SCHEDULED, label: 'Scheduled' },
  { value: CAMPAIGN_STATUSES.ACTIVE, label: 'Active' },
  { value: CAMPAIGN_STATUSES.PAUSED, label: 'Paused' },
  { value: CAMPAIGN_STATUSES.COMPLETED, label: 'Completed' },
  { value: CAMPAIGN_STATUSES.CANCELLED, label: 'Cancelled' },
];

// Completed and cancelled campaigns cannot change status (server rule).
export const CLOSED_CAMPAIGN_STATUSES = [
  CAMPAIGN_STATUSES.COMPLETED,
  CAMPAIGN_STATUSES.CANCELLED,
];

export const PROMOTION_SORT_OPTIONS = [
  { value: 'startDate', label: 'Start date' },
  { value: 'endDate', label: 'End date' },
  { value: 'name', label: 'Name' },
  { value: 'discountValue', label: 'Discount' },
  { value: 'createdAt', label: 'Created' },
];

export const CAMPAIGN_SORT_OPTIONS = [
  { value: 'startDate', label: 'Start date' },
  { value: 'endDate', label: 'End date' },
  { value: 'name', label: 'Name' },
  { value: 'createdAt', label: 'Created' },
];

export const PAGE_SIZE = 10;

export const MANAGER_ROLES = ['Staff', 'Administrator'];
