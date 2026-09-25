import { describe, expect, it } from 'vitest';
import { normalizeApiError } from './apiErrorUtils.js';

describe('api error utilities', () => {
  it('formats validation errors for users', () => {
    expect(
      normalizeApiError({
        errors: {
          sku: ['SKU is already in use.'],
          price: ['Price must not be negative.'],
        },
      }),
    ).toBe('SKU is already in use. Price must not be negative.');
  });

  it('formats unauthorized and forbidden responses clearly', () => {
    expect(normalizeApiError({ status: 401 })).toBe(
      'Your session has expired or you are not signed in. Please sign in again.',
    );
    expect(normalizeApiError({ status: 403 })).toBe(
      'You do not have permission to perform this action.',
    );
  });

  it('hides server internals and stack traces', () => {
    expect(
      normalizeApiError({
        status: 500,
        detail: 'System.Exception: database failed',
      }),
    ).toBe('The server could not complete the request. Please try again later.');

    expect(
      normalizeApiError({
        message: 'System.Exception at Service.cs:line 42',
      }),
    ).toBe('The request could not be completed. Please try again.');
  });
});
