import { describe, expect, it } from 'vitest';
import { PROMOTION_TYPES } from './marketingConstants';
import {
  EMPTY_CAMPAIGN_FORM,
  EMPTY_PROMOTION_FORM,
  formToPromotionPayload,
  fromApiDate,
  promotionState,
  promotionToForm,
  toApiDate,
  validateCampaignForm,
  validatePromotionForm,
  withActiveState,
} from './marketingUtils';

const validPromotionForm = {
  ...EMPTY_PROMOTION_FORM,
  name: 'Autumn Deal',
  type: String(PROMOTION_TYPES.PERCENTAGE),
  discountValue: '15',
  startDate: '2026-10-01',
  endDate: '2026-10-31',
  productIds: ['p1'],
};

const apiPromotion = {
  id: 'promo-1',
  name: 'Pizza 20% Off',
  description: null,
  type: PROMOTION_TYPES.PERCENTAGE,
  discountValue: 20,
  startDate: '2026-09-01T00:00:00Z',
  endDate: '2026-12-31T00:00:00Z',
  isActive: true,
  campaignId: 'camp-1',
  productIds: ['p1', 'p2'],
  categoryIds: [],
};

describe('dates', () => {
  it('converts between date inputs and UTC API dates', () => {
    expect(toApiDate('2026-10-01')).toBe('2026-10-01T00:00:00Z');
    expect(toApiDate('')).toBeNull();
    expect(fromApiDate('2026-10-01T00:00:00Z')).toBe('2026-10-01');
    expect(fromApiDate(null)).toBe('');
  });
});

describe('validatePromotionForm', () => {
  it('accepts a valid form', () => {
    expect(validatePromotionForm(validPromotionForm)).toEqual({});
  });

  it('requires name, dates and a target', () => {
    const errors = validatePromotionForm({ ...EMPTY_PROMOTION_FORM, discountValue: '10' });

    expect(errors.name).toBe('Name is required.');
    expect(errors.startDate).toBe('Start date is required.');
    expect(errors.endDate).toBe('End date is required.');
    expect(errors.productIds).toBe('Choose at least one product or category.');
  });

  it.each([
    [PROMOTION_TYPES.PERCENTAGE, '0'],
    [PROMOTION_TYPES.PERCENTAGE, '100.5'],
    [PROMOTION_TYPES.FIXED_AMOUNT, '0'],
    [PROMOTION_TYPES.FREE_SHIPPING, '-1'],
  ])('rejects type %s with discount %s', (type, discountValue) => {
    const errors = validatePromotionForm({
      ...validPromotionForm,
      type: String(type),
      discountValue,
    });

    expect(errors.discountValue).toBeTruthy();
  });

  it('allows free shipping without targets or discount', () => {
    const errors = validatePromotionForm({
      ...validPromotionForm,
      type: String(PROMOTION_TYPES.FREE_SHIPPING),
      discountValue: '',
      productIds: [],
    });

    expect(errors).toEqual({});
  });

  it('rejects an end date before the start date', () => {
    const errors = validatePromotionForm({ ...validPromotionForm, endDate: '2026-09-30' });

    expect(errors.endDate).toBe('End date must be on or after the start date.');
  });

  it('rejects dates outside the selected campaign', () => {
    const campaign = { startDate: '2026-10-01T00:00:00Z', endDate: '2026-10-15T00:00:00Z' };

    const errors = validatePromotionForm(validPromotionForm, campaign);

    expect(errors.campaignId).toMatch(/within the campaign/);
  });
});

describe('promotion payloads', () => {
  it('builds the API payload with numbers, UTC dates and null campaign', () => {
    expect(formToPromotionPayload(validPromotionForm)).toEqual({
      name: 'Autumn Deal',
      description: null,
      type: 0,
      discountValue: 15,
      startDate: '2026-10-01T00:00:00Z',
      endDate: '2026-10-31T00:00:00Z',
      isActive: true,
      campaignId: null,
      productIds: ['p1'],
      categoryIds: [],
    });
  });

  it('round-trips an API promotion through the form', () => {
    const payload = formToPromotionPayload(promotionToForm(apiPromotion));

    expect(payload.startDate).toBe(apiPromotion.startDate);
    expect(payload.endDate).toBe(apiPromotion.endDate);
    expect(payload.productIds).toEqual(['p1', 'p2']);
    expect(payload.campaignId).toBe('camp-1');
  });

  it('withActiveState only changes isActive', () => {
    const payload = withActiveState(apiPromotion, false);

    expect(payload.isActive).toBe(false);
    expect(payload.name).toBe('Pizza 20% Off');
    expect(payload.discountValue).toBe(20);
  });
});

describe('promotionState', () => {
  const now = new Date('2026-10-15T12:00:00Z');

  it.each([
    [{ ...apiPromotion, isActive: false }, 'Inactive'],
    [{ ...apiPromotion, startDate: '2026-11-01T00:00:00Z' }, 'Upcoming'],
    [{ ...apiPromotion, endDate: '2026-10-01T00:00:00Z' }, 'Ended'],
    [apiPromotion, 'Running'],
  ])('labels %#', (promotion, label) => {
    expect(promotionState(promotion, now).label).toBe(label);
  });
});

describe('validateCampaignForm', () => {
  it('requires name and dates, and ordered dates', () => {
    expect(validateCampaignForm(EMPTY_CAMPAIGN_FORM)).toMatchObject({
      name: 'Name is required.',
      startDate: 'Start date is required.',
      endDate: 'End date is required.',
    });

    expect(validateCampaignForm({
      ...EMPTY_CAMPAIGN_FORM,
      name: 'Spring',
      startDate: '2027-04-30',
      endDate: '2027-04-01',
    })).toEqual({ endDate: 'End date must be on or after the start date.' });
  });
});
