import {
  CAMPAIGN_STATUS_OPTIONS,
  PROMOTION_TYPE_OPTIONS,
  PROMOTION_TYPES,
} from './marketingConstants';

// ---- Dates ---------------------------------------------------------------
// The API stores UTC instants. The UI edits whole dates (yyyy-mm-dd) and sends
// them as UTC midnight, the same convention as the seeded data.

export function toApiDate(dateInput) {
  return dateInput ? `${dateInput}T00:00:00Z` : null;
}

export function fromApiDate(apiDate) {
  return apiDate ? String(apiDate).slice(0, 10) : '';
}

const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeZone: 'UTC',
});

export function formatDate(apiDate) {
  return apiDate ? dateFormatter.format(new Date(apiDate)) : '—';
}

const moneyFormatter = new Intl.NumberFormat('en-LK', {
  style: 'currency',
  currency: 'LKR',
});

export function formatMoney(value) {
  return moneyFormatter.format(Number(value) || 0);
}

// ---- Labels --------------------------------------------------------------

export function promotionTypeLabel(type) {
  return PROMOTION_TYPE_OPTIONS.find((o) => o.value === type)?.label ?? 'Unknown';
}

export function campaignStatusLabel(status) {
  return CAMPAIGN_STATUS_OPTIONS.find((o) => o.value === status)?.label ?? 'Unknown';
}

export function formatDiscount(promotion) {
  switch (promotion.type) {
    case PROMOTION_TYPES.PERCENTAGE:
      return `${promotion.discountValue}% off`;
    case PROMOTION_TYPES.FIXED_AMOUNT:
      return `${formatMoney(promotion.discountValue)} off`;
    case PROMOTION_TYPES.FREE_SHIPPING:
      return 'Free shipping';
    default:
      return promotionTypeLabel(promotion.type);
  }
}

// Where a promotion is in its lifecycle, relative to `now`.
export function promotionState(promotion, now = new Date()) {
  if (!promotion.isActive) {
    return { label: 'Inactive', tone: 'muted' };
  }

  if (new Date(promotion.startDate) > now) {
    return { label: 'Upcoming', tone: 'info' };
  }

  if (new Date(promotion.endDate) < now) {
    return { label: 'Ended', tone: 'muted' };
  }

  return { label: 'Running', tone: 'success' };
}

export function campaignStatusTone(status) {
  return ['muted', 'info', 'success', 'warning', 'muted', 'danger'][status] ?? 'muted';
}

// ---- Promotion form -------------------------------------------------------

export const EMPTY_PROMOTION_FORM = {
  name: '',
  description: '',
  type: String(PROMOTION_TYPES.PERCENTAGE),
  discountValue: '',
  startDate: '',
  endDate: '',
  isActive: true,
  campaignId: '',
  productIds: [],
  categoryIds: [],
};

export function promotionToForm(promotion) {
  return {
    name: promotion.name ?? '',
    description: promotion.description ?? '',
    type: String(promotion.type),
    discountValue: String(promotion.discountValue ?? ''),
    startDate: fromApiDate(promotion.startDate),
    endDate: fromApiDate(promotion.endDate),
    isActive: Boolean(promotion.isActive),
    campaignId: promotion.campaignId ?? '',
    productIds: [...(promotion.productIds ?? [])],
    categoryIds: [...(promotion.categoryIds ?? [])],
  };
}

export function formToPromotionPayload(form) {
  return {
    name: form.name.trim(),
    description: form.description.trim() || null,
    type: Number(form.type),
    discountValue: form.discountValue === '' ? 0 : Number(form.discountValue),
    startDate: toApiDate(form.startDate),
    endDate: toApiDate(form.endDate),
    isActive: form.isActive,
    campaignId: form.campaignId || null,
    productIds: form.productIds,
    categoryIds: form.categoryIds,
  };
}

// Builds the full PUT body for a promotion with only `isActive` changed.
export function withActiveState(promotion, isActive) {
  return { ...formToPromotionPayload(promotionToForm(promotion)), isActive };
}

// Mirrors the server rules so most mistakes are caught before submitting.
export function validatePromotionForm(form, campaign) {
  const errors = {};
  const type = Number(form.type);
  const value = Number(form.discountValue);

  if (!form.name.trim()) {
    errors.name = 'Name is required.';
  } else if (form.name.trim().length > 200) {
    errors.name = 'Name must be 200 characters or fewer.';
  }

  if (form.description.length > 2000) {
    errors.description = 'Description must be 2000 characters or fewer.';
  }

  if (form.discountValue !== '' && Number.isNaN(value)) {
    errors.discountValue = 'Enter a number.';
  } else if (type === PROMOTION_TYPES.PERCENTAGE && (!(value > 0) || value > 100)) {
    errors.discountValue = 'Percentage must be greater than 0 and at most 100.';
  } else if (type === PROMOTION_TYPES.FIXED_AMOUNT && !(value > 0)) {
    errors.discountValue = 'Fixed discount must be greater than zero.';
  } else if (value < 0) {
    errors.discountValue = 'Discount cannot be negative.';
  }

  if (!form.startDate) {
    errors.startDate = 'Start date is required.';
  }

  if (!form.endDate) {
    errors.endDate = 'End date is required.';
  } else if (form.startDate && form.endDate < form.startDate) {
    errors.endDate = 'End date must be on or after the start date.';
  }

  const isPriceDiscount =
    type === PROMOTION_TYPES.PERCENTAGE || type === PROMOTION_TYPES.FIXED_AMOUNT;

  if (isPriceDiscount && form.productIds.length === 0 && form.categoryIds.length === 0) {
    errors.productIds = 'Choose at least one product or category.';
  }

  if (campaign && form.startDate && form.endDate && !errors.endDate) {
    const campaignStart = fromApiDate(campaign.startDate);
    const campaignEnd = fromApiDate(campaign.endDate);

    if (form.startDate < campaignStart || form.endDate > campaignEnd) {
      errors.campaignId =
        `Dates must fall within the campaign (${campaignStart} to ${campaignEnd}).`;
    }
  }

  return errors;
}

// ---- Campaign form --------------------------------------------------------

export const EMPTY_CAMPAIGN_FORM = {
  name: '',
  description: '',
  startDate: '',
  endDate: '',
  status: '0',
};

export function campaignToForm(campaign) {
  return {
    name: campaign.name ?? '',
    description: campaign.description ?? '',
    startDate: fromApiDate(campaign.startDate),
    endDate: fromApiDate(campaign.endDate),
    status: String(campaign.status),
  };
}

export function formToCampaignPayload(form) {
  return {
    name: form.name.trim(),
    description: form.description.trim() || null,
    startDate: toApiDate(form.startDate),
    endDate: toApiDate(form.endDate),
    status: Number(form.status),
  };
}

export function validateCampaignForm(form) {
  const errors = {};

  if (!form.name.trim()) {
    errors.name = 'Name is required.';
  } else if (form.name.trim().length > 200) {
    errors.name = 'Name must be 200 characters or fewer.';
  }

  if (form.description.length > 2000) {
    errors.description = 'Description must be 2000 characters or fewer.';
  }

  if (!form.startDate) {
    errors.startDate = 'Start date is required.';
  }

  if (!form.endDate) {
    errors.endDate = 'End date is required.';
  } else if (form.startDate && form.endDate < form.startDate) {
    errors.endDate = 'End date must be on or after the start date.';
  }

  return errors;
}
