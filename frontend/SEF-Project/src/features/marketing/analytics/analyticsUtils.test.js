import { describe, expect, it } from 'vitest';
import {
  defaultGranularity,
  GRANULARITY,
  niceTicks,
  percentChange,
  previousRange,
  resolveDateRange,
  toTrendPoints,
} from './analyticsUtils';

const NOW = new Date('2026-10-15T12:34:00Z');

describe('resolveDateRange', () => {
  it('covers whole UTC days up to and including today for presets', () => {
    expect(resolveDateRange({ range: '7' }, NOW)).toEqual({
      from: '2026-10-09T00:00:00.000Z',
      to: '2026-10-16T00:00:00.000Z',
      days: 7,
    });
  });

  it('treats a custom end date as inclusive (half-open API range)', () => {
    expect(resolveDateRange({ range: 'custom', from: '2026-10-01', to: '2026-10-10' }, NOW)).toEqual({
      from: '2026-10-01T00:00:00.000Z',
      to: '2026-10-11T00:00:00.000Z',
      days: 10,
    });
  });

  it('falls back to 30 days for an incomplete custom range', () => {
    expect(resolveDateRange({ range: 'custom', from: '2026-10-01', to: '' }, NOW).days).toBe(30);
  });
});

describe('previousRange', () => {
  it('returns the preceding period of the same length', () => {
    expect(previousRange({ from: '2026-10-09T00:00:00.000Z', to: '2026-10-16T00:00:00.000Z' })).toEqual({
      from: '2026-10-02T00:00:00.000Z',
      to: '2026-10-09T00:00:00.000Z',
    });
  });
});

describe('defaultGranularity', () => {
  it('uses days up to ~3 months and months beyond', () => {
    expect(defaultGranularity(90)).toBe(GRANULARITY.DAY);
    expect(defaultGranularity(365)).toBe(GRANULARITY.MONTH);
  });
});

describe('percentChange', () => {
  it('is null without a baseline', () => {
    expect(percentChange(100, 0)).toBeNull();
    expect(percentChange(150, 100)).toBe(50);
  });
});

describe('niceTicks', () => {
  it('produces round ticks that cover the maximum', () => {
    expect(niceTicks(5300)).toEqual([0, 2000, 4000, 6000]);
    expect(niceTicks(0)).toEqual([0, 1]);
  });
});

describe('toTrendPoints', () => {
  it('maps API points for the selected metric with period labels', () => {
    const response = {
      granularity: GRANULARITY.MONTH,
      points: [
        { periodStart: '2026-09-01T00:00:00Z', orderCount: 1, unitsSold: 4, netRevenue: 4800 },
        { periodStart: '2026-10-01T00:00:00Z', orderCount: 2, unitsSold: 4, netRevenue: 5300 },
      ],
    };

    expect(toTrendPoints(response, 'revenue')).toEqual([
      { key: '2026-09-01T00:00:00Z', label: 'Sept 2026', value: 4800 },
      { key: '2026-10-01T00:00:00Z', label: 'Oct 2026', value: 5300 },
    ]);
    expect(toTrendPoints(response, 'orders').map((p) => p.value)).toEqual([1, 2]);
  });
});
