import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { normalizeApiError } from './apiErrorUtils.js';

describe('api error utilities', () => {
  it('formats validation errors for users', () => {
    assert.equal(
      normalizeApiError({
        errors: {
          sku: ['SKU is already in use.'],
          price: ['Price must not be negative.'],
        },
      }),
      'SKU is already in use. Price must not be negative.',
    );
  });

  it('formats unauthorized and forbidden responses clearly', () => {
    assert.equal(
      normalizeApiError({ status: 401 }),
      'Your session has expired or you are not signed in. Please sign in again.',
    );
    assert.equal(
      normalizeApiError({ status: 403 }),
      'You do not have permission to perform this action.',
    );
  });

  it('hides server internals and stack traces', () => {
    assert.equal(
      normalizeApiError({
        status: 500,
        detail: 'System.Exception: database failed',
      }),
      'The server could not complete the request. Please try again later.',
    );

    assert.equal(
      normalizeApiError({
        message: 'System.Exception at Service.cs:line 42',
      }),
      'The request could not be completed. Please try again.',
    );
  });
});
