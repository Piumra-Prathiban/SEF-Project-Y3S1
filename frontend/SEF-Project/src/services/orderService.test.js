import { describe, expect, it } from 'vitest';
import { buildOrderQuery } from './orderService';

describe('buildOrderQuery', () => {
  it('returns an empty string when nothing is set', () => {
    expect(buildOrderQuery()).toBe('');
    expect(buildOrderQuery({ page: undefined, pageSize: null })).toBe('');
  });

  it('includes pagination, filtering, and sorting parameters', () => {
    const params = new URLSearchParams(
      buildOrderQuery({
        page: 2,
        pageSize: 50,
        sortBy: 'total',
        sortDirection: 'asc',
        status: 'Pending',
        orderNumber: 'ORD-1001',
        customerId: 42,
      }),
    );

    expect(params.get('page')).toBe('2');
    expect(params.get('pageSize')).toBe('50');
    expect(params.get('sortBy')).toBe('total');
    expect(params.get('sortDirection')).toBe('asc');
    expect(params.get('status')).toBe('Pending');
    expect(params.get('orderNumber')).toBe('ORD-1001');
    expect(params.get('customerId')).toBe('42');
  });

  it('ISO-formats Date values for from/to', () => {
    const from = new Date('2026-09-01T04:30:00Z');
    const to = new Date('2026-09-30T23:59:59.999Z');

    const params = new URLSearchParams(buildOrderQuery({ from, to }));

    expect(params.get('from')).toBe(from.toISOString());
    expect(params.get('to')).toBe(to.toISOString());
  });

  it('skips empty strings, nulls, and undefined values', () => {
    const params = new URLSearchParams(
      buildOrderQuery({
        status: '',
        orderNumber: null,
        customerId: undefined,
        page: 1,
      }),
    );

    expect(params.get('page')).toBe('1');
    expect(params.has('status')).toBe(false);
    expect(params.has('orderNumber')).toBe(false);
    expect(params.has('customerId')).toBe(false);
  });
});
